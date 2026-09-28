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

        [Test]
        public void CheckoutRequiresEveryField()
        {
            Assert.IsFalse(CheckoutValidation.IsComplete("Анна", "+374", " ", "1234", "12/30", "123"));
            Assert.IsTrue(CheckoutValidation.IsComplete("Анна", "+374", "Ереван", "1234", "12/30", "123"));
        }

        [Test]
        public void CheckoutDigitMasksFollowTypedDigits()
        {
            Assert.AreEqual("+7 912 345-67-89", CheckoutValidation.FormatPhone("7a 912-345 67+89"));
            Assert.AreEqual("+7 912", CheckoutValidation.FormatPhone("+7 912 "));
            Assert.AreEqual("12/34", CheckoutValidation.FormatExpiry("1a2/34"));
            Assert.AreEqual("12", CheckoutValidation.FormatExpiry("12/"));
        }
    }
}
