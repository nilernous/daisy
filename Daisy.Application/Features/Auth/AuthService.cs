using Daisy.Application.Common;
using Daisy.Application.Features.Cart;
using Daisy.Application.Interfaces;
using Daisy.Domain.Common;
using Daisy.Domain.Entities;
using Daisy.Domain.Enums;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Daisy.Application.Features.Auth;

public sealed class AuthService
{
    private const string ForgotPasswordMessage = "If the email exists, a reset link has been sent.";

    private readonly IApplicationDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly IEmailService _emailService;
    private readonly CartService _cartService;
    private readonly TimeProvider _time;
    private readonly PasswordResetOptions _resetOptions;
    private readonly ILogger<AuthService> _logger;
    private readonly IValidator<RegisterRequest> _registerValidator;
    private readonly IValidator<LoginRequest> _loginValidator;
    private readonly IValidator<ForgotPasswordRequest> _forgotValidator;
    private readonly IValidator<ResetPasswordRequest> _resetValidator;
    private readonly IValidator<ChangePasswordRequest> _changeValidator;

    public AuthService(
        IApplicationDbContext db,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        IEmailService emailService,
        CartService cartService,
        TimeProvider time,
        IOptions<PasswordResetOptions> resetOptions,
        ILogger<AuthService> logger,
        IValidator<RegisterRequest> registerValidator,
        IValidator<LoginRequest> loginValidator,
        IValidator<ForgotPasswordRequest> forgotValidator,
        IValidator<ResetPasswordRequest> resetValidator,
        IValidator<ChangePasswordRequest> changeValidator)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _emailService = emailService;
        _cartService = cartService;
        _time = time;
        _resetOptions = resetOptions.Value;
        _logger = logger;
        _registerValidator = registerValidator;
        _loginValidator = loginValidator;
        _forgotValidator = forgotValidator;
        _resetValidator = resetValidator;
        _changeValidator = changeValidator;
    }

    /// <summary>UC-01. New accounts are always Customers (BR-07); emails are unique (BR-01).</summary>
    public async Task<UserSummaryDto> RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        await _registerValidator.ValidateAndThrowAsync(request, ct);
        var email = ValidationRules.NormalizeEmail(request.Email);

        if (await _db.Users.AnyAsync(u => u.Email == email, ct))
        {
            throw new ConflictException(ErrorCodes.EmailAlreadyExists, "This email is already registered.");
        }

        var user = new User
        {
            FullName = request.FullName.Trim(),
            Email = email,
            PasswordHash = _passwordHasher.HashPassword(request.Password),
            Role = UserRole.Customer,
            CreatedAt = _time.GetVietnamNow(),
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);
        return UserSummaryDto.From(user);
    }

    /// <summary>UC-02. Merges the guest cart when a session id is given (UC-31).</summary>
    public async Task<AuthResultDto> LoginAsync(LoginRequest request, string? guestSessionId, CancellationToken ct)
    {
        await _loginValidator.ValidateAndThrowAsync(request, ct);
        var email = ValidationRules.NormalizeEmail(request.Email);
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);

        if (user is null || !_passwordHasher.VerifyPassword(user.PasswordHash, request.Password))
        {
            throw new UnauthorizedException(ErrorCodes.InvalidCredentials, "Invalid email or password.");
        }

        if (user.IsLocked)
        {
            throw new ForbiddenException(ErrorCodes.AccountLocked, "Your account is locked.");
        }

        var result = IssueTokens(user);
        await _db.SaveChangesAsync(ct);

        if (!string.IsNullOrWhiteSpace(guestSessionId))
        {
            await _cartService.MergeGuestCartAsync(guestSessionId, user.UserId, ct);
        }

        return result;
    }

    /// <summary>UC-03. Clears the stored refresh token (BR-04).</summary>
    public async Task LogoutAsync(int userId, CancellationToken ct)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == userId, ct);
        if (user is null)
        {
            return;
        }

        user.RevokeRefreshToken();
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>UC-04. Rotates the refresh token.</summary>
    public async Task<AuthResultDto> RefreshAsync(RefreshRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            throw new UnauthorizedException(ErrorCodes.InvalidRefreshToken, "The refresh token is invalid or expired.");
        }

        var hash = _tokenService.HashToken(request.RefreshToken);
        var user = await _db.Users.FirstOrDefaultAsync(u => u.RefreshToken == hash, ct);
        var now = _time.GetVietnamNow();

        if (user is null || !user.HasValidRefreshToken(hash, now))
        {
            throw new UnauthorizedException(ErrorCodes.InvalidRefreshToken, "The refresh token is invalid or expired.");
        }

        if (user.IsLocked)
        {
            user.RevokeRefreshToken();
            await _db.SaveChangesAsync(ct);
            throw new ForbiddenException(ErrorCodes.AccountLocked, "Your account is locked.");
        }

        var result = IssueTokens(user);
        await _db.SaveChangesAsync(ct);
        return result;
    }

    /// <summary>UC-05. Same response whether or not the email exists (BR-05).</summary>
    public async Task<MessageDto> ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken ct)
    {
        await _forgotValidator.ValidateAndThrowAsync(request, ct);
        var email = ValidationRules.NormalizeEmail(request.Email);
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);
        if (user is null)
        {
            return new MessageDto(ForgotPasswordMessage);
        }

        var token = _tokenService.CreateRandomToken();
        var now = _time.GetVietnamNow();
        user.SetResetPasswordToken(_tokenService.HashToken(token), now.AddMinutes(_resetOptions.TokenMinutes));
        await _db.SaveChangesAsync(ct);

        var separator = _resetOptions.ResetUrl.Contains('?') ? '&' : '?';
        var link = $"{_resetOptions.ResetUrl}{separator}token={Uri.EscapeDataString(token)}";
        try
        {
            await _emailService.SendPasswordResetAsync(user.Email, user.FullName, link, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Sending the password reset email failed.");
            throw new ServiceUnavailableException(ErrorCodes.EmailServiceUnavailable, "The email could not be sent. Please try again later.");
        }

        return new MessageDto(ForgotPasswordMessage);
    }

    /// <summary>UC-06. The token is single-use; the refresh token is revoked (BR-05).</summary>
    public async Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct)
    {
        await _resetValidator.ValidateAndThrowAsync(request, ct);
        var hash = _tokenService.HashToken(request.Token);
        var user = await _db.Users.FirstOrDefaultAsync(u => u.ResetPasswordToken == hash, ct);
        var now = _time.GetVietnamNow();

        if (user is null || !user.HasValidResetToken(hash, now))
        {
            throw new BadRequestException(ErrorCodes.InvalidResetToken, "The reset link is invalid or has expired. Please request a new one.");
        }

        user.ChangePasswordHash(_passwordHasher.HashPassword(request.NewPassword), now);
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>UC-07. Requires the current password (BR-06).</summary>
    public async Task ChangePasswordAsync(int userId, ChangePasswordRequest request, CancellationToken ct)
    {
        await _changeValidator.ValidateAndThrowAsync(request, ct);
        var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == userId, ct)
            ?? throw new NotFoundException();

        if (!_passwordHasher.VerifyPassword(user.PasswordHash, request.CurrentPassword))
        {
            throw new BadRequestException(ErrorCodes.IncorrectPassword, "The current password is incorrect.");
        }

        user.ChangePasswordHash(_passwordHasher.HashPassword(request.NewPassword), _time.GetVietnamNow());
        await _db.SaveChangesAsync(ct);
    }

    private AuthResultDto IssueTokens(User user)
    {
        var access = _tokenService.CreateAccessToken(user);
        var refresh = _tokenService.CreateRefreshToken();
        user.SetRefreshToken(_tokenService.HashToken(refresh.Token), refresh.ExpiresAt);
        return new AuthResultDto(access.Token, access.ExpiresAt, refresh.Token, refresh.ExpiresAt, UserSummaryDto.From(user));
    }
}
