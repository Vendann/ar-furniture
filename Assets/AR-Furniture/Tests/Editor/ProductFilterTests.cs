using System.Linq;
using NUnit.Framework;

namespace ARFurniture.Tests
{
    public sealed class ProductFilterTests
    {
        [Test]
        public void CategoryAndMaximumPriceAreAppliedTogether()
        {
            var products = new[]
            {
                new Product { name = "Дешёвый диван", category = ProductCategory.Sofas, price = 40_000 },
                new Product { name = "Дорогой диван", category = ProductCategory.Sofas, price = 80_000 },
                new Product { name = "Кресло", category = ProductCategory.Armchairs, price = 20_000 }
            };

            var result = ProductFilter.Apply(products, ProductCategory.Sofas, 50_000).Select(product => product.name);

            CollectionAssert.AreEqual(new[] { "Дешёвый диван" }, result);
        }
    }
}
