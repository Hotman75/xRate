using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using xRate.Core.Helpers;
using xRate.Core.Services;

namespace xRateExt.Pages;

internal sealed partial class SettingsFormContent : FormContent
{
    private readonly SettingsService _settingsService = new();

    public SettingsFormContent()
    {
        Reload();
    }

    public void Reload()
    {
        var settings = _settingsService.GetSettings(true);

        var choicesJson = string.Join(",", CurrencyMapper.SupportedCurrencies.Select(item =>
        {
            string displayTitle = $"{item.IsoCode} - {item.Name}";

            if (item.IsEmoji) displayTitle = $"{item.Emoji} {displayTitle}";

            return $"{{\"title\":\"{JsonEncodedText.Encode(displayTitle)}\",\"value\":\"{item.IsoCode}\"}}";
        }));

        TemplateJson = $$"""
        {
            "type": "AdaptiveCard",
            "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
            "version": "1.5",
            "body": [
                {
                    "type": "TextBlock",
                    "text": "Settings",
                    "size": "Large",
                    "weight": "Bolder"
                },
                {
                    "type": "TextBlock",
                    "text": "Default 'From' Currency",
                    "wrap": true
                },
                {
                    "type": "Input.ChoiceSet",
                    "id": "DefaultFrom",
                    "value": "{{settings.DefaultFrom}}",
                    "choices": [{{choicesJson}}]
                },
                {
                    "type": "TextBlock",
                    "text": "Default 'To' Currency",
                    "wrap": true
                },
                {
                    "type": "Input.ChoiceSet",
                    "id": "DefaultTo",
                    "value": "{{settings.DefaultTo}}",
                    "choices": [{{choicesJson}}]
                }
            ],
            "actions": [
                {
                    "type": "Action.Submit",
                    "title": "Save Settings",
                    "style": "positive"
                }
            ]
        }
        """;
    }

    public override ICommandResult SubmitForm(string inputs, string data)
    {
        var formInput = JsonNode.Parse(inputs)?.AsObject();
        if (formInput != null)
        {
            var from = formInput["DefaultFrom"]?.GetValue<string>();
            var to = formInput["DefaultTo"]?.GetValue<string>();

            if (!string.IsNullOrEmpty(from) && !string.IsNullOrEmpty(to))
            {
                var currentSettings = _settingsService.GetSettings();
                currentSettings.DefaultFrom = from;
                currentSettings.DefaultTo = to;

                _ = _settingsService.SaveSettingsAsync(currentSettings);
            }
        }

        return CommandResult.GoHome();
    }
}

internal sealed partial class SettingsPage : ContentPage
{
    private readonly SettingsFormContent _form = new();

    public SettingsPage()
    {
        this.Name = "Settings";
        this.Icon = new IconInfo("\uE713");
    }

    public override IContent[] GetContent()
    {
        _form.Reload();
        return [_form];
    }
}