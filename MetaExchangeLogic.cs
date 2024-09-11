using MetaExchangeWPF;
using Newtonsoft.Json;
using System.IO;

public class MetaExchangeLogic
{
    // Class representing the final execution plan, including the best price and a list of exchange orders
    public class ExecutionPlan
    {
        // The best price achieved for the order (weighted average)
        public decimal BestPrice { get; set; }
        // List of individual exchange orders executed to achieve the plan
        public List<ExchangeOrder>? ExchangeOrders { get; set; }
    }

    /// <summary>
    /// Determines the best execution plan based on the type of order (Buy or Sell), the amount, and available exchanges.
    /// </summary>
    /// <param name="orderType">"Buy" or "Sell" indicating the type of transaction</param>
    /// <param name="amount">The amount of crypto to buy or sell</param>
    /// <param name="exchanges">The list of available exchanges and their order books</param>
    /// <param name="exceedsLimit">Indicates if the order amount exceeds available funds</param>
    /// <param name="totalAvailableFunds">The total available funds in all exchanges (Crypto for Buy, Euro for Sell)</param>
    /// <returns>An execution plan with the best price and list of orders</returns>
    public static ExecutionPlan GetBestExecution(string orderType, decimal amount, List<ExchangeData> exchanges, out bool exceedsLimit, out decimal totalAvailableFunds)
    {
        // Initialize an empty execution plan
        var executionPlan = new ExecutionPlan
        {
            ExchangeOrders = new List<ExchangeOrder>()
        };

        // List of relevant orders, based on order type (Buy or Sell)
        List<ExchangeOrder> relevantOrders;

        if (orderType == "Buy")
        {
            // For Buy: Collect all ask orders (Crypto being sold)
            relevantOrders = exchanges
                .SelectMany(e => e.OrderBook.Asks.Select(a => new ExchangeOrder { Exchange = e.Id, Price = a.Order.Price, Amount = a.Order.Amount }))
                .OrderBy(o => o.Price)  // Sort by price in ascending order (lowest first)
                .ToList();

            // Calculate the total available Crypto across all exchanges
            totalAvailableFunds = exchanges.Sum(e => e.AvailableFunds.Crypto);

            // Check if the requested amount exceeds the available Crypto
            exceedsLimit = amount > totalAvailableFunds;
        }
        else // Sell
        {
            // For Sell: Collect all bid orders (buyers)
            relevantOrders = exchanges
                .SelectMany(e => e.OrderBook.Bids.Select(b => new ExchangeOrder { Exchange = e.Id, Price = b.Order.Price, Amount = b.Order.Amount }))
                .OrderByDescending(o => o.Price)  // Sort by price in descending order (highest first)
                .ToList();

            // Calculate the total Euro we would get for selling the requested amount of Crypto
            decimal totalEuroForCrypto = 0m;
            decimal totalAccumulatedAmount = 0m;

            foreach (var order in relevantOrders)
            {
                if (totalAccumulatedAmount >= amount)
                    break;

                decimal remainingAmount = amount - totalAccumulatedAmount;
                decimal orderAmountToTake = Math.Min(remainingAmount, order.Amount);

                // Add the potential Euro earned from this order
                totalEuroForCrypto += orderAmountToTake * order.Price;
                totalAccumulatedAmount += orderAmountToTake;
            }

            // Compare the total Euro we'd get with the available Euro funds in all exchanges
            totalAvailableFunds = exchanges.Sum(e => e.AvailableFunds.Euro);
            exceedsLimit = totalEuroForCrypto > totalAvailableFunds;
        }

        // If the amount exceeds the limit, return an empty plan
        if (exceedsLimit)
        {
            return executionPlan;
        }

        // Execute the order if limits are not exceeded
        decimal finalAccumulatedAmount = 0m;
        var availableFunds = exchanges.ToDictionary(e => e.Id, e => orderType == "Buy" ? e.AvailableFunds.Crypto : e.AvailableFunds.Euro);

        foreach (var order in relevantOrders)
        {
            if (finalAccumulatedAmount >= amount)
                break;

            // Ensure the exchange has enough funds for the transaction
            if (availableFunds[order.Exchange] <= 0)
                continue;

            decimal remainingAmount = amount - finalAccumulatedAmount;
            decimal orderAmountToTake = Math.Min(remainingAmount, Math.Min(order.Amount, availableFunds[order.Exchange]));

            // Get the exchange data for the current order
            var exchange = exchanges.FirstOrDefault(e => e.Id == order.Exchange);
            if (exchange == null) continue;

            // Check for sufficient Euro in the exchange for Sell orders
            if (orderType == "Sell")
            {
                decimal euroRequired = orderAmountToTake * order.Price;
                if (exchange.AvailableFunds.Euro < euroRequired)
                    continue;

                exchange.AvailableFunds.Euro -= euroRequired;  // Deduct Euro from exchange
            }

            // Deduct the available funds on the exchange
            availableFunds[order.Exchange] -= orderAmountToTake;

            // Update the exchange data for Buy orders
            if (orderType == "Buy")
            {
                exchange.AvailableFunds.Euro += orderAmountToTake * order.Price;  // Increase Euro
                exchange.AvailableFunds.Crypto -= orderAmountToTake;  // Decrease Crypto

                // Update the order book by reducing or removing fulfilled ask orders
                var askOrder = exchange.OrderBook.Asks.FirstOrDefault(a => a.Order.Price == order.Price && a.Order.Amount == order.Amount);
                if (askOrder != null)
                {
                    askOrder.Order.Amount -= orderAmountToTake;
                    if (askOrder.Order.Amount <= 0)
                        exchange.OrderBook.Asks.Remove(askOrder);
                }
            }
            else // Sell
            {
                exchange.AvailableFunds.Crypto += orderAmountToTake;  // Increase Crypto for sell

                // Update the order book by reducing or removing fulfilled bid orders
                var bidOrder = exchange.OrderBook.Bids.FirstOrDefault(b => b.Order.Price == order.Price && b.Order.Amount == order.Amount);
                if (bidOrder != null)
                {
                    bidOrder.Order.Amount -= orderAmountToTake;
                    if (bidOrder.Order.Amount <= 0)
                        exchange.OrderBook.Bids.Remove(bidOrder);
                }
            }

            // Save the updated exchange data to the JSON file
            string filePath = Path.Combine(Directory.GetCurrentDirectory(), "exchanges", $"{exchange.Id}.json");
            File.WriteAllText(filePath, JsonConvert.SerializeObject(exchange, Formatting.Indented));

            // Add the executed order to the plan
            executionPlan.ExchangeOrders.Add(new ExchangeOrder
            {
                Exchange = order.Exchange,
                Price = order.Price,
                Amount = orderAmountToTake
            });

            finalAccumulatedAmount += orderAmountToTake;
        }

        // Calculate the weighted average price of all executed orders
        if (executionPlan.ExchangeOrders.Any())
        {
            decimal totalCost = executionPlan.ExchangeOrders.Sum(o => o.Price * o.Amount);
            executionPlan.BestPrice = totalCost / finalAccumulatedAmount;
        }
        else
        {
            executionPlan.BestPrice = 0; // No orders executed
        }

        return executionPlan;
    }
}
