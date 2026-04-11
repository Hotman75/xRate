# xRate

Instant currency conversion tool. Available as a standalone desktop app or integrated directly into Microsoft PowerToys, xRate provides fast and simple real-time exchange rates.

<img src="https://github.com/user-attachments/assets/bbde1e80-f83e-4903-a804-b9da248c698f" height="600" alt="xRate App Demo" />

![cmdpal](https://github.com/user-attachments/assets/c3ef3e4c-05fe-497e-b76d-e8040c8019f8)

## 🖥 Desktop Application

The desktop app serves as the main interface for conversions and the control center for your preferences.

* **Quick Conversion**: Standard interface to convert any amount between 150+ currencies.
* **Offline**: Rates are saved locally so you can keep converting even without internet.
* **Updates**: Rates are refreshed automatically to ensure accuracy.

<img width="384" height="471" alt="xRate_Desktop_App" src="https://github.com/user-attachments/assets/572d410c-fec2-47e5-acdc-38cd61f6ecf6" />


## ⚡ Command Palette

xRate integrates into the PowerToys Command Palette for near-instant access.

* **Flexible Syntax**: Supports multiple ways to query: `100 USD to EUR`, `100$`, `100 EUR GBP`, or just `100` to use your default currencies.
* **Smart Recognition**: Automatically detects currency symbols ($ , €, £, ¥, ...) and ISO codes.
* **Extended Commands**:
    * **Supported Currencies**: View all available currencies.
    * **Settings**: Set the selected currency as your new default "From" or "To" without opening the app.

<img width="784" height="473" alt="cmdpal" src="https://github.com/user-attachments/assets/a526d0a3-d43c-4502-b3f7-ed0a904ef4bb" />


## 🔄 Shared Settings

The app and the extension share the same configuration. 
* Any change to your preferred currencies in the Desktop App is applied to the Command Palette immediately.
* Conversely, updating your defaults via the Command Palette updates the App settings in real-time.

<img width="784" height="473" alt="cmdpal_settings" src="https://github.com/user-attachments/assets/4b3aa56a-9666-4622-a255-24e80c565d28" />


## 📊 Data Source

Exchange rates are provided by the [Frankfurter API](https://frankfurter.dev/), which utilizes open data from the European Central Bank. Rates are updated daily.

## 📥 Installation

1. **Download xRate**: Available on the Microsoft Store for automatic updates.  
   [**xRate on the Microsoft Store**](https://apps.microsoft.com/detail/9nm38wvxbcrq)
2. **PowerToys**: To use the Command Palette extension, Microsoft PowerToys must be installed on your system.  
   [Download PowerToys here](https://learn.microsoft.com/windows/powertoys/install)

---

**Author**: Othman AMOR  
**License**: MIT
