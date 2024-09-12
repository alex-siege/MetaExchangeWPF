# MetaExchange Application

## Overview

This application enables users to execute market orders for Bitcoin (BTC) and provides an execution plan based on the best available prices across all provided exchanges. Additionally, the application visualizes the state of order books and available funds on each exchange, which can be updated manually through the interface.

## Features

- **Market Order Execution**: Execute buy or sell orders for BTC based on existing exchange data.
- **Order Book Visualization**: View the current state of order books across multiple exchanges, including bids (buy orders) and asks (sell orders).
- **Dynamic Exchange Updates**: Refresh the available funds and order books at any time by reloading the exchange data.
- **JSON-Based Exchange Data**: The application reads exchange data from JSON files located in the working directory.

## How to Use

### Step 1: Launch the Application

After executing the solution, the main window of the application will open. The layout is divided into three main sections:

- **Upper Left Section**: This contains the controls for executing market orders.
- **Upper Right Section**: This is where the execution plan will be displayed after placing an order.
- **Lower Section**: This area visualizes the order books for all exchanges, displaying both bids and asks along with the available funds.

### Step 2: Execute a Market Order

1. **Enter the Order Details**:
   - **Order Type**: Select either "Buy" or "Sell" from the dropdown menu.
   - **Amount**: Enter the amount of BTC you wish to buy or sell.
   
2. **Execute the Order**:  
   Once you've entered the necessary details, press the **'Execute Order'** button. The application will compute the best execution plan, which will be displayed in the **Result TextBox** in the upper right section of the window.

### Step 3: Update Exchange Data

1. **Update Order Books and Funds**:  
   Press the **'Update Exchanges'** button to reload the latest exchange data. This action will:
   - Update the visualized plots for bids and asks in the lower section of the window.
   - Refresh the currently available funds for both Crypto and Euro on each exchange.

### JSON Files

The application reads exchange data from JSON files located in the `exchanges` folder, which is in the working directory of the solution. Each JSON file contains information about an exchange's order book (bids and asks) and available funds (Crypto and Euro). You can replace these files with downsized test files which are located inside the `exchanges/test_exchanges_backup` folder.

## Project Structure

- **MainWindow.xaml**: Contains the layout and controls for the GUI.
- **MainWindow.xaml.cs**: Implements the functionality for order execution, exchange updates, and data visualization.
- **MetaExchangeLogic.cs**: Handles the business logic for calculating the best execution plan based on available order book data.
- **ExchangeModels.cs**: Defines the data models for exchanges, order books, and orders.
- **exchanges/**: A folder containing JSON files that store the exchange data used by the application.
