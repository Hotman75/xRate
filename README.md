# xRate

Instant currency conversion tool. Available as a standalone desktop app or integrated directly into Microsoft PowerToys, xRate provides fast and simple real-time exchange rates.

<img width="404" height="531" alt="desktop" src="https://github.com/user-attachments/assets/b28db84d-52e6-40cf-b088-329704e412b1" />
<br><br>
<img width="784" height="473" alt="cmdpal" src="https://github.com/user-attachments/assets/af713db9-0417-4c00-8dd6-3bd441f5a339" />

## 🖥 Desktop Application

The desktop app serves as the main interface for conversions and the control center for your preferences.

* **Quick Conversion**: Standard interface to convert any amount between 150+ currencies.
* **Math Support**: Perform basic arithmetic (+, -, *, /) directly in the amount fields to calculate totals before converting.
* **Offline**: Rates are saved locally so you can keep converting even without internet.
* **Updates**: Rates are refreshed automatically to ensure accuracy.

<img width="404" height="531" alt="desktop2" src="https://github.com/user-attachments/assets/5d87caf3-45d2-43fa-8883-3a7951a4d7c6" />


## ⚡ Command Palette

xRate integrates into the PowerToys Command Palette for near-instant access.

* **Flexible Syntax & Math**: Supports complex queries like (100+20)*1.1 USD to EUR, 100$, 100 EUR GBP, or just 100 to use your default currencies.
* **Smart Recognition**: Automatically detects currency symbols ($ , €, £, ¥, ...) and ISO codes.
* **Extended Commands**:
    * **Supported Currencies**: View all available currencies.
    * **Settings**: Set the selected currency as your new default "From" or "To" without opening the app.

<img width="784" height="473" alt="cmdpal2" src="https://github.com/user-attachments/assets/28f156c2-92d9-4189-9769-0273f07cec5f" />


## 🔄 Shared Settings

The app and the extension share the same configuration. 
* Any change to your preferred currencies in the Desktop App is applied to the Command Palette immediately.
* Conversely, updating your defaults via the Command Palette updates the App settings in real-time.

<img width="784" height="473" alt="cmdpal_settings" src="https://github.com/user-attachments/assets/4b3aa56a-9666-4622-a255-24e80c565d28" />


## 📊 Data Source

Exchange rates are provided by the [Frankfurter API](https://frankfurter.dev/), which utilizes open data from the European Central Bank. Rates are updated daily.

## 📥 Installation

1. **Download xRate Desktop App**: Available on the Microsoft Store for automatic updates.  
   [**xRate - Quic Currency Converter**](https://apps.microsoft.com/detail/9nm38wvxbcrq)
2. **PowerToys**: To use the Command Palette extension, Microsoft PowerToys must be installed on your system.  
   [Download PowerToys here](https://learn.microsoft.com/windows/powertoys/install)

---

**Author**: Othman AMOR  
**License**: MIT
