using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
//using System.Windows.Shapes;
using System.IO;
using Newtonsoft.Json;
using OxyPlot.Axes;
using OxyPlot.Series;
using OxyPlot.Wpf;
using OxyPlot;
using System.Diagnostics;

namespace MetaExchangeWPF
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private List<ExchangeData> loadedExchanges = new List<ExchangeData>(); // Store all exchanges here

        public MainWindow()
        {
            InitializeComponent();
        }

        // Function to load and deserialize exchange data from the "exchanges" folder
        private List<ExchangeData> LoadExchangesFromFolder()
        {
            List<ExchangeData> exchanges = new List<ExchangeData>();
            string folderPath = Path.Combine(Directory.GetCurrentDirectory(), "exchanges");

            // Check if the folder exists
            if (Directory.Exists(folderPath))
            {
                // Load all JSON files from the 'exchanges' folder
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
                            exchanges.Add(exchange); // Add each exchange to the list
                        }
                    }
                    catch (Exception ex)
                    {
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

        private void ExecuteOrder_Click(object sender, RoutedEventArgs e)
        {
            loadedExchanges = LoadExchangesFromFolder(); // Load exchanges from the folder

            string orderType = ((ComboBoxItem)OrderTypeComboBox.SelectedItem).Content.ToString();
            decimal amount;

            if (decimal.TryParse(AmountTextBox.Text, out amount))
            {
                // Variables to check for limit and available funds
                bool exceedsLimit;
                decimal totalAvailableFunds;

                // Get the execution plan
                var executionPlan = MetaExchangeLogic.GetBestExecution(orderType, amount, loadedExchanges, out exceedsLimit, out totalAvailableFunds);

                if (exceedsLimit)
                {
                    // Display a message indicating the limit has been exceeded
                    ResultTextBlock.Text = $"The amount you want to {orderType.ToLower()} exceeds the available funds of all exchanges.\n" +
                                           $"Requested: {amount}\n" +
                                           $"Available: {totalAvailableFunds}";
                }
                else
                {
                    // Display the result of the execution plan
                    ResultTextBlock.Text = $"Best Price: {executionPlan.BestPrice}\n" +
                                           $"Orders:\n" +
                                           string.Join("\n", executionPlan.ExchangeOrders.Select(o => $"Exchange: {o.Exchange}, Price: {o.Price}, Amount: {o.Amount}"));
                }
            }
            else
            {
                ResultTextBlock.Text = "Please enter a valid amount.";
            }
        }



        private void LoadExchanges_Click(object sender, RoutedEventArgs e)
        {
            // Clear the panel before loading new order books
            OrderBooksPanel.Children.Clear();

            // Path to the "exchanges" folder
            string folderPath = Path.Combine(Directory.GetCurrentDirectory(), "exchanges");

            // Load all JSON files in the folder
            if (Directory.Exists(folderPath))
            {
                string[] files = Directory.GetFiles(folderPath, "*.json");

                foreach (string file in files)
                {
                    try
                    {
                        // Read and deserialize the JSON file
                        var jsonData = File.ReadAllText(file);
                        var exchangeData = JsonConvert.DeserializeObject<ExchangeData>(jsonData);

                        // Ensure a new plot is created for each exchange
                        DisplayOrderBookChart(exchangeData);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error loading file: {file}\n{ex.Message}");
                    }
                }
            }
            else
            {
                MessageBox.Show("Exchanges folder not found.");
            }
        }

        // Function to display a bar chart for each order book
        private void DisplayOrderBookChart(ExchangeData exchange)
        {
            // Update the titles to include available funds for the exchange
            var modelAsks = new PlotModel { Title = $"Asks - {exchange.Id} (Crypto Available: {exchange.AvailableFunds.Crypto})" };
            var modelBids = new PlotModel { Title = $"Bids - {exchange.Id} (Euro Available: {exchange.AvailableFunds.Euro})" };

            // Sort asks in ascending order (lowest price first)
            var sortedAsks = exchange.OrderBook.Asks.OrderBy(a => a.Order.Price).ToList();

            // Sort bids in ascending order (lowest price first)
            var sortedBids = exchange.OrderBook.Bids.OrderBy(b => b.Order.Price).ToList(); // Ascending order for bids

            // Create a series for asks
            var askSeries = new BarSeries
            {
                Title = "Asks",
                FillColor = OxyColors.Red,
                YAxisKey = "PriceAxis",
                XAxisKey = "AmountAxis"
            };

            // Create a series for bids
            var bidSeries = new BarSeries
            {
                Title = "Bids",
                FillColor = OxyColors.Green,
                YAxisKey = "PriceAxis",
                XAxisKey = "AmountAxis"
            };

            // Set up the CategoryAxis for the Y Axis (price levels) for asks
            var priceAxisAsks = new CategoryAxis
            {
                Position = AxisPosition.Left,
                Title = "Price",
                Key = "PriceAxis",
                FontSize = 15
            };

            // Add bars and corresponding price labels for asks
            foreach (var ask in sortedAsks)
            {
                askSeries.Items.Add(new BarItem((double)ask.Order.Amount));
                priceAxisAsks.Labels.Add(ask.Order.Price.ToString());
            }

            // Set up the CategoryAxis for the Y Axis (price levels) for bids
            var priceAxisBids = new CategoryAxis
            {
                Position = AxisPosition.Left,
                Title = "Price",
                Key = "PriceAxis",
                FontSize = 15
            };

            // Add bars and corresponding price labels for bids
            foreach (var bid in sortedBids)
            {
                bidSeries.Items.Add(new BarItem((double)bid.Order.Amount));
                priceAxisBids.Labels.Add(bid.Order.Price.ToString());
            }

            // Set up the LinearAxis for the X Axis (amounts of BTC)
            var amountAxisAsks = new LinearAxis
            {
                Position = AxisPosition.Bottom,
                Title = "Amount",
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
                Height = 300, // Height of the chart
                Margin = new Thickness(0, 0, 0, 0) // Spacing between charts
            };

            var plotViewBids = new PlotView
            {
                Model = modelBids,
                Height = 300, // Height of the chart
                Margin = new Thickness(0, 0, 0, 0) // Spacing between charts
            };

            // Create a grid to stack the bid and ask charts vertically
            var containerGrid = new Grid
            {
                Margin = new Thickness(10, 10, 10, 0),
                Height = 610,
                Width = 500
            };

            // Define two rows for the grid
            containerGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            containerGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Place the ask chart in the first row
            Grid.SetRow(plotViewAsks, 0);
            containerGrid.Children.Add(plotViewAsks);

            // Place the bid chart in the second row
            Grid.SetRow(plotViewBids, 1);
            containerGrid.Children.Add(plotViewBids);

            // Add the container grid to the OrderBooksPanel
            OrderBooksPanel.Children.Add(containerGrid);

            // Debugging: Verify that charts are added by logging
            Debug.WriteLine($"Charts for exchange {exchange.Id} added.");
        }

        private void AmountTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {

        }
    }
}