using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Polly;
using Polly.Fallback;
using Polly.Retry;
using Polly.Timeout;

public record InventoryItem
{
    public int ItemId { get; init; }
    public string Name { get; init; }
    public decimal? Price { get; init; }
    public int Quantity { get; init; }
    public string Category { get; init; } = ""; // New field
    public DateTime? LastRestockDate { get; init; } // New field
}

public class InventoryProcessor
{
    public static void Main(string[] args)
    {
        Console.WriteLine("--- Inventory Processing Started ---");

        var inventoryList = GetInventoryData();

        long totalItems = inventoryList.Sum(item => (long)item.Quantity);
        Console.WriteLine($"\nTotal Available Inventory Count: {totalItems}");

        var itemsForDiscount = inventoryList
            .Where(item => item.Name.StartsWith("A", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var outdatedItems = FindOutdatedInventory(inventoryList);
        Console.WriteLine(
            $"\nOutdated items (Category 'Peripherals', not restocked in 90 days): {outdatedItems.Count}"
        );
        foreach (var item in outdatedItems)
        {
            Console.WriteLine(
                $" - {item.Name} ({item.Category}) - Last Restock: {item.LastRestockDate:d}"
            );
        }

        Console.WriteLine($"\nItems starting with 'A' (after discount calculation):");

        decimal discountFactor = GetDiscountFactorSafe();

        foreach (var item in itemsForDiscount)
        {
            var newPrice = item.Price is not null ? item.Price.Value * discountFactor : 0m;
            Console.WriteLine(
                $" - {item.Name} (ID: {item.ItemId}) - New Price: {newPrice:C} (Original price was NULL)"
            );
        }

        Console.WriteLine("\n--- Inventory Processing Complete ---");
    }

    private static List<InventoryItem> FindOutdatedInventory(List<InventoryItem> items)
    {
        var Filtered_Data = items
            .Where(i =>
                i.Category == "Peripherals"
                && i.LastRestockDate != null
                && (DateTime.Now - i.LastRestockDate.Value).TotalDays > 90
            )
            .ToList();
        // TODO: Implement logic here using LINQ to filter items where:
        // 1. Category is "Peripherals"
        // 2. LastRestockDate is NOT NULL
        // 3. LastRestockDate is older than 90 days (from DateTime.Now)
        return Filtered_Data;
    }

    private static decimal GetDiscountFactor()
    {
        Thread.Sleep(50);
        return 0.95m; // 5% discount factor
    }

    private static decimal GetDiscountFactorSafe()
    {
        var pipeline = new ResiliencePipelineBuilder<decimal>()
            .AddFallback(
                new FallbackStrategyOptions<decimal>
                {
                    FallbackAction = static args =>
                    {
                        Console.WriteLine("Fallback activated: defaulting to 1.0");
                        return Outcome.FromResultAsValueTask(1.0m);
                    }
                }
            )
            .AddRetry(
                new RetryStrategyOptions<decimal>
                {
                    MaxRetryAttempts = 3,
                    Delay = TimeSpan.FromMilliseconds(50),
                    OnRetry = static args =>
                    {
                        Console.WriteLine(
                            $"Retry #{args.AttemptNumber} due to {args.Outcome.Exception?.Message ?? "unknown error"}"
                        );
                        return default;
                    }
                }
            )
            .AddTimeout(
                new TimeoutStrategyOptions
                {
                    Timeout = TimeSpan.FromMilliseconds(100),
                    OnTimeout = static args =>
                    {
                        Console.WriteLine($"Timeout after {args.Timeout.TotalMilliseconds} ms");
                        return default;
                    }
                }
            )
            .Build();

        return pipeline.Execute(
            static (CancellationToken ct) => GetDiscountFactor(),
            CancellationToken.None
        );
    }

    private static List<InventoryItem> GetInventoryData()
    {
        DateTime today = DateTime.Now;

        return new List<InventoryItem>
        {
            new InventoryItem
            {
                ItemId = 101,
                Name = "Laptop Adapter",
                Price = 49.99m,
                Quantity = 50,
                Category = "Accessories",
                LastRestockDate = today.AddDays(-10)
            },
            new InventoryItem
            {
                ItemId = 102,
                Name = "Wireless Mouse",
                Price = 19.99m,
                Quantity = 120,
                Category = "Peripherals",
                LastRestockDate = today.AddDays(-150)
            }, // Outdated
            new InventoryItem
            {
                ItemId = 103,
                Name = "USB-C Hub",
                Price = 75.00m,
                Quantity = 30,
                Category = "Accessories",
                LastRestockDate = today.AddDays(-50)
            },
            new InventoryItem
            {
                ItemId = 104,
                Name = "Adjustable Stand",
                Price = null,
                Quantity = 80,
                Category = "Ergonomics",
                LastRestockDate = null
            }, // Null Price & Null Date
            new InventoryItem
            {
                ItemId = 105,
                Name = "Monitor Cable",
                Price = 9.99m,
                Quantity = 200,
                Category = "Peripherals",
                LastRestockDate = today.AddDays(-30)
            }, // Not Outdated
            new InventoryItem
            {
                ItemId = 106,
                Name = "Apple Pencil",
                Price = 99.00m,
                Quantity = 40,
                Category = "Accessories",
                LastRestockDate = today.AddDays(-100)
            }, // Starts with 'A'
            new InventoryItem
            {
                ItemId = 107,
                Name = "Gaming Headset",
                Price = 150.00m,
                Quantity = 60,
                Category = "Peripherals",
                LastRestockDate = today.AddDays(-200)
            }, // Outdated
            new InventoryItem
            {
                ItemId = 108,
                Name = "Projector",
                Price = 450.00m,
                Quantity = 10,
                Category = "Displays",
                LastRestockDate = today.AddDays(-5)
            }
        };
    }
}
