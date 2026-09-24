using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ARFurniture
{
    public enum ProductCategory
    {
        Sofas,
        Armchairs,
        Tables
    }

    [Serializable]
    public sealed class Product
    {
        public string name;
        public ProductCategory category;
        public int price;
        public Sprite image;
        public GameObject prefab;
    }

    [CreateAssetMenu(menuName = "AR Furniture/Product Catalog", fileName = "ProductCatalog")]
    public sealed class ProductCatalog : ScriptableObject
    {
        public Product[] products;
    }

    public static class ProductFilter
    {
        public static IEnumerable<Product> Apply(
            IEnumerable<Product> products,
            ProductCategory? category,
            int maximumPrice)
        {
            return products.Where(product =>
                (!category.HasValue || product.category == category.Value) &&
                product.price <= maximumPrice);
        }
    }
}
