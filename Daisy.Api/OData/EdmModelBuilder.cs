using Daisy.Application.Features.Orders;
using Daisy.Application.Features.Products;
using Daisy.Application.Features.Reviews;
using Daisy.Application.Features.Users;
using Microsoft.OData.Edm;
using Microsoft.OData.ModelBuilder;

namespace Daisy.Api.OData;

/// <summary>Entity sets of API_DESIGN §7. Property names are camelCase, matching the JSON.</summary>
public static class EdmModelBuilder
{
    public static IEdmModel Build()
    {
        var builder = new ODataConventionModelBuilder();
        builder.EnableLowerCamelCase();

        builder.ComplexType<VariantOptionDto>();
        builder.EntitySet<ProductListDto>("products").EntityType.HasKey(p => p.Id);
        builder.EntitySet<OrderAdminListDto>("orders").EntityType.HasKey(o => o.Id);
        builder.EntitySet<ReviewAdminDto>("reviews").EntityType.HasKey(r => r.Id);
        builder.EntitySet<UserListDto>("users").EntityType.HasKey(u => u.Id);

        return builder.GetEdmModel();
    }
}
