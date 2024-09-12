namespace MetaExchangeWPF
{
    // Classes needed to deserialize .json into structs.

    /// <summary>
    /// Represents the main exchange data that includes ID, available funds, and the order book.
    /// </summary>
    public class ExchangeData
    {
        // Unique identifier for the exchange.
        public string? Id { get; set; }

        // The available funds (Crypto and Euro) for this exchange.
        public AvailableFunds? AvailableFunds { get; set; }

        // The order book of the exchange, containing bids and asks.
        public OrderBook? OrderBook { get; set; }
    }

    /// <summary>
    /// Represents the available funds (Crypto and Euro) for an exchange.
    /// </summary>
    public class AvailableFunds
    {
        // The amount of cryptocurrency (BTC) available in the exchange.
        public decimal Crypto { get; set; }

        // The amount of Euro available in the exchange.
        public decimal Euro { get; set; }
    }

    /// <summary>
    /// Represents the order book, containing lists of bids and asks for an exchange.
    /// </summary>
    public class OrderBook
    {
        // List of bid orders (buyers) in the exchange's order book.
        public List<BidAsk>? Bids { get; set; }

        // List of ask orders (sellers) in the exchange's order book.
        public List<BidAsk>? Asks { get; set; }
    }

    /// <summary>
    /// Represents either a bid or ask order in the exchange's order book.
    /// </summary>
    public class BidAsk
    {
        // The order details for either a bid (buy) or ask (sell) order.
        public Order? Order { get; set; }
    }

    /// <summary>
    /// Represents a detailed order including its unique ID, amount, and price.
    /// </summary>
    public class Order
    {
        // Unique identifier for the order.
        public string? Id { get; set; }

        // The amount of cryptocurrency involved in the order.
        public decimal Amount { get; set; }

        // The price at which the order is placed.
        public decimal Price { get; set; }
    }
}
