using System.Windows;
using System.Windows.Controls;
using System.IO;
using Newtonsoft.Json;
using OxyPlot.Axes;
using OxyPlot.Series;
using OxyPlot.Wpf;
using OxyPlot;
using System.Diagnostics;
using System.Windows.Media;

namespace MetaExchangeWPF
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // List to store all the loaded exchange data
        private List<ExchangeData> loadedExchanges = new List<ExchangeData>();

        public MainWindow()
        {
            InitializeComponent();
            // Automatically load exchanges upon starting the application
            LoadExchanges_Click(this, null);
        }

        /// <summary>
        /// Loads and deserializes exchange data from JSON files located in the "exchanges" folder.
        /// </summary>
        /// <returns>List of exchange data loaded from the folder</returns>
        private List<ExchangeData> LoadExchangesFromFolder()
        {
            List<ExchangeData> exchanges = new List<ExchangeData>();
            string folderPath = Path.Combine(Directory.GetCurrentDirectory(), "exchanges");

            // Check if the exchanges folder exists
            if (Directory.Exists(folderPath))
            {
                // Get all JSON files in the folder
                var exchangeFiles = Directory.GetFiles(folderPath, "*.json");

                foreach (var file in exchangeFiles)
                {
                    try
                    {
                        // Read and deserialize each JSON file into an ExchangeData object
                        string jsonContent = File.ReadAllText(file);
                        ExchangeData exchange = JsonConvert.DeserializeObject<ExchangeData>(jsonContent);

                        if (exchange != null)
                        {
                            exchanges.Add(exchange); // Add the exchange data to the list
                        }
                    }
                    catch (Exception ex)
                    {
                        // Handle any errors during file reading or deserialization
                        MessageBox.Show($"Error loading exchange data from {file}: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            else
            {
                MessageBox.Show("The exchanges folder could not be found.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            return exchanges;
        }

        /// <summary>
        /// Handles the event for executing an order. Loads exchanges and processes the order to get the best execution plan.
        /// </summary>
        private void ExecuteOrder_Click(object sender, RoutedEventArgs e)
        {
            // Load the latest exchange data
            loadedExchanges = LoadExchangesFromFolder();

            // Get the order type ("Buy" or "Sell")
            string orderType = ((ComboBoxItem)OrderTypeComboBox.SelectedItem).Content.ToString();
            decimal amount;

            // Validate the entered amount
            if (decimal.TryParse(AmountTextBox.Text, out amount))
            {
                // Variables to check if the amount exceeds the available funds and to store total available funds
                bool exceedsLimit;
                decimal totalAvailableFunds;

                // Get the best execution plan based on the order type, amount, and exchange data
                var executionPlan = MetaExchangeLogic.GetBestExecution(orderType, amount, loadedExchanges, out exceedsLimit, out totalAvailableFunds);

                if (exceedsLimit)
                {
                    // Display a message if the requested amount exceeds available funds
                    ResultTextBlock.Text = $"The amount you want to {orderType.ToLower()} exceeds the available funds of all exchanges to cover your transaction or there are not enough orders.\n" +
                                           $"Requested amount: {amount}\n" +
                                           $"Available Funds on Exchanges: {totalAvailableFunds}";
                }
                else
                {
                    // Display the execution plan results in the result text block
                    ResultTextBlock.Text = $"Best Price (Average): {executionPlan.BestPrice}\n" +
                                           $"\nOrders:" +
                                           string.Join("\n\n", executionPlan.ExchangeOrders.Select(o => $"Exchange: {o.Exchange}, Price: {o.Price}, Amount: {o.Amount}"));
                }
            }
            else
            {
                ResultTextBlock.Text = "Please enter a valid amount.";
            }
        }

        /// <summary>
        /// Handles the event for loading exchange data and displays the order book charts for each exchange.
        /// </summary>
        private void LoadExchanges_Click(object sender, RoutedEventArgs e)
        {
            // Clear the panel before loading new order books
            OrderBooksPanel.Children.Clear();

            // Path to the "exchanges" folder
            string folderPath = Path.Combine(Directory.GetCurrentDirectory(), "exchanges");

            // Check if the exchanges folder exists
            if (Directory.Exists(folderPath))
            {
                string[] files = Directory.GetFiles(folderPath, "*.json");

                foreach (string file in files)
                {
                    try
                    {
                        // Read and deserialize the exchange data from the JSON file
                        var jsonData = File.ReadAllText(file);
                        var exchangeData = JsonConvert.DeserializeObject<ExchangeData>(jsonData);

                        // Display the order book chart for the exchange
                        DisplayOrderBookChart(exchangeData);
                    }
                    catch (Exception ex)
                    {
                        // Handle any errors during the loading process
                        MessageBox.Show($"Error loading file: {file}\n{ex.Message}");
                    }
                }
            }
            else
            {
                MessageBox.Show("Exchanges folder not found.");
            }
        }

        /// <summary>
        /// Displays the order book (asks and bids) for a given exchange using bar charts.
        /// </summary>
        /// <param name="exchange">Exchange data containing order books and available funds</param>
        private void DisplayOrderBookChart(ExchangeData exchange)
        {
            // Create a grid to hold the available funds and the order book charts
            var containerGrid = new Grid
            {
                Margin = new Thickness(10, 10, 10, 0),
                Height = 610,
                Width = 500
            };

            // Define rows for available funds, asks chart, and bids chart
            containerGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            containerGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            containerGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Display the available funds at the top of the container grid
            var fundsTextBlock = new TextBlock
            {
                Text = $"Available Funds:\n Euro = {exchange.AvailableFunds.Euro},\n Crypto = {exchange.AvailableFunds.Crypto}",
                FontSize = 18,
                FontWeight = System.Windows.FontWeights.Bold,
                Foreground = new SolidColorBrush(Colors.DarkBlue),
                Margin = new Thickness(0, 0, 0, 10)
            };

            // Place the available funds TextBlock in the first row
            Grid.SetRow(fundsTextBlock, 0);
            containerGrid.Children.Add(fundsTextBlock);

            // Prepare the PlotModel for asks
            var modelAsks = new PlotModel { Title = $"Ask-Prices of {exchange.Id}" };
            var modelBids = new PlotModel { Title = $"Bid-Prices of {exchange.Id}" };

            // Sort and process the ask orders
            var sortedAsks = exchange.OrderBook.Asks.OrderBy(a => a.Order.Price).ToList();
            var sortedBids = exchange.OrderBook.Bids.OrderBy(b => b.Order.Price).ToList();

            // Create bar series for asks and bids
            var askSeries = new BarSeries
            {
                Title = "Asks",
                FillColor = OxyColors.Brown,
                YAxisKey = "PriceAxis",
                XAxisKey = "AmountAxis"
            };

            var bidSeries = new BarSeries
            {
                Title = "Bids",
                FillColor = OxyColors.DarkCyan,
                YAxisKey = "PriceAxis",
                XAxisKey = "AmountAxis"
            };

            // Set up the Y-axis (price levels) for asks
            var priceAxisAsks = new CategoryAxis
            {
                Position = AxisPosition.Left,
                Key = "PriceAxis",
                FontSize = 14
            };

            // Reduce the number of labels displayed on the Y-axis for asks
            int labelIntervalAsks = Math.Max(1, sortedAsks.Count / 10);
            for (int i = 0; i < sortedAsks.Count; i++)
            {
                askSeries.Items.Add(new BarItem((double)sortedAsks[i].Order.Amount));

                if (i % labelIntervalAsks == 0)
                {
                    priceAxisAsks.Labels.Add(sortedAsks[i].Order.Price.ToString());
                }
                else
                {
                    priceAxisAsks.Labels.Add(""); // Hide non-interval labels
                }
            }

            // Set up the Y-axis (price levels) for bids
            var priceAxisBids = new CategoryAxis
            {
                Position = AxisPosition.Left,
                Key = "PriceAxis",
                FontSize = 14
            };

            // Reduce the number of labels displayed on the Y-axis for bids
            int labelIntervalBids = Math.Max(1, sortedBids.Count / 10);
            for (int i = 0; i < sortedBids.Count; i++)
            {
                bidSeries.Items.Add(new BarItem((double)sortedBids[i].Order.Amount));

                if (i % labelIntervalBids == 0)
                {
                    priceAxisBids.Labels.Add(sortedBids[i].Order.Price.ToString());
                }
                else
                {
                    priceAxisBids.Labels.Add(""); // Hide non-interval labels
                }
           

            }

            // Set up the LinearAxis for the X Axis (amounts of BTC)
            var amountAxisAsks = new LinearAxis
            {
                Position = AxisPosition.Bottom,
                Key = "AmountAxis"
            };

            var amountAxisBids = new LinearAxis
            {
                Position = AxisPosition.Bottom,
                Title = "Amount",
                Key = "AmountAxis"
            };

            // Add axes to both models
            modelAsks.Axes.Add(priceAxisAsks); // Category (Price) on Y Axis for asks
            modelAsks.Axes.Add(amountAxisAsks); // Linear (Amount) on X Axis for asks

            modelBids.Axes.Add(priceAxisBids); // Category (Price) on Y Axis for bids
            modelBids.Axes.Add(amountAxisBids); // Linear (Amount) on X Axis for bids

            // Add the series to the models
            modelAsks.Series.Add(askSeries);
            modelBids.Series.Add(bidSeries);

            // Create the plot views for asks and bids
            var plotViewAsks = new PlotView
            {
                Model = modelAsks,
                Height = 270, // Height of the chart
                Margin = new Thickness(0, 0, 0, 0) // Spacing between charts
            };

            var plotViewBids = new PlotView
            {
                Model = modelBids,
                Height = 270, // Height of the chart
                Margin = new Thickness(0, 0, 0, 0) // Spacing between charts
            };

            // Place the ask chart in the second row
            Grid.SetRow(plotViewAsks, 1);
            containerGrid.Children.Add(plotViewAsks);

            // Place the bid chart in the third row
            Grid.SetRow(plotViewBids, 2);
            containerGrid.Children.Add(plotViewBids);

            // Add the container grid to the OrderBooksPanel
            OrderBooksPanel.Children.Add(containerGrid);

            // Debugging: Verify that charts are added by logging
            Debug.WriteLine($"Charts for exchange {exchange.Id} added.");
        }
    }
}