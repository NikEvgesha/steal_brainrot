using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[CreateAssetMenu(fileName = "GoogleSheetData", menuName = "Google Sheets/Data")]
public class GoogleSheetData : ScriptableObject
{
    [SerializeField] private string sheetId;
    [SerializeField] private string sheetName;
    [SerializeField] private string credentialsPath = "Assets/Resources/credentials.json";
    [SerializeField] private LocalizationData localizationData;

    private SheetsService GetSheetsService(bool readOnly)
    {
        try
        {
            var scopes = readOnly
                ? new[] { SheetsService.Scope.SpreadsheetsReadonly }
                : new[] { SheetsService.Scope.Spreadsheets };

            var credential = GoogleCredential.FromFile(credentialsPath)
                .CreateScoped(scopes);

            return new SheetsService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "UnityGoogleSheets",
            });
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Failed to initialize Sheets Service: {ex.Message}");
            return null;
        }
    }

    public void DownloadAndParseSheet()
    {
        if (string.IsNullOrEmpty(sheetId) || string.IsNullOrEmpty(sheetName))
        {
            Debug.LogError("Sheet ID or Sheet Name is empty!");
            return;
        }

        try
        {
            var range = $"{sheetName}!A1:Z";
            var service = GetSheetsService(readOnly: true);

            if (service == null)
            {
                Debug.LogError("Failed to initialize Google Sheets service.");
                return;
            }

            var request = service.Spreadsheets.Values.Get(sheetId, range);
            ValueRange response = request.Execute();
            IList<IList<object>> values = response.Values;

            if (values != null && values.Count > 0)
            {
                List<string[]> data = new List<string[]>();
                foreach (var row in values)
                    data.Add(row.Select(cell => cell?.ToString() ?? string.Empty).ToArray());

                if (localizationData != null)
                {
                    localizationData.SetData(data);
                    Debug.Log("Localization data updated via Google Sheets API!");
#if UNITY_EDITOR
                    EditorUtility.SetDirty(localizationData);
                    AssetDatabase.SaveAssets();
#endif
                }
                else
                {
                    Debug.LogWarning("LocalizationData is not assigned!");
                }
            }
            else
            {
                Debug.LogWarning("No data found in the specified range.");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Failed to fetch data: {ex.Message}");
        }
    }

    public void UploadLocalizationToSheet()
    {
        if (string.IsNullOrEmpty(sheetId) || string.IsNullOrEmpty(sheetName))
        {
            Debug.LogError("Sheet ID or Sheet Name is empty!");
            return;
        }

        if (localizationData == null)
        {
            Debug.LogError("LocalizationData is not assigned!");
            return;
        }

        if (localizationData.Languages == null || localizationData.Languages.Count == 0)
        {
            Debug.LogError("LocalizationData has no languages.");
            return;
        }

        try
        {
            var service = GetSheetsService(readOnly: false);
            if (service == null)
            {
                Debug.LogError("Failed to initialize Google Sheets service.");
                return;
            }

            var values = BuildSheetValues();
            if (values.Count == 0)
            {
                Debug.LogWarning("No localization rows prepared for upload.");
                return;
            }

            var clearRequest = new ClearValuesRequest();
            service.Spreadsheets.Values.Clear(clearRequest, sheetId, $"{sheetName}!A:ZZ").Execute();

            var valueRange = new ValueRange { Values = values };
            var updateRequest = service.Spreadsheets.Values.Update(valueRange, sheetId, $"{sheetName}!A1");
            updateRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.RAW;
            var updateResult = updateRequest.Execute();

            Debug.Log($"Localization uploaded to Google Sheet. Rows: {values.Count}, UpdatedCells: {updateResult?.UpdatedCells ?? 0}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Failed to upload localization data: {ex.Message}");
        }
    }

    private IList<IList<object>> BuildSheetValues()
    {
        var rows = new List<IList<object>>();
        var header = new List<object> { "Key" };

        for (var i = 0; i < localizationData.Languages.Count; i++)
            header.Add(localizationData.Languages[i] ?? string.Empty);

        rows.Add(header);

        if (localizationData.Entries == null)
            return rows;

        foreach (var entry in localizationData.Entries)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.Key))
                continue;

            var row = new List<object> { entry.Key };
            for (var i = 0; i < localizationData.Languages.Count; i++)
            {
                var value = (entry.Translations != null && i < entry.Translations.Count)
                    ? entry.Translations[i]
                    : string.Empty;
                row.Add(value ?? string.Empty);
            }

            rows.Add(row);
        }

        return rows;
    }

    public void OpenSheetInBrowser()
    {
        if (string.IsNullOrEmpty(sheetId))
        {
            Debug.LogError("Sheet ID is empty!");
            return;
        }

        Application.OpenURL($"https://docs.google.com/spreadsheets/d/{sheetId}");
    }
}
