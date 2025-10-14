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
    [SerializeField] private string sheetId; // ID таблицы
    [SerializeField] private string sheetName; // Имя листа (например, "Sheet1")
    [SerializeField] private string credentialsPath = "Assets/Resources/credentials.json"; // Путь к JSON
    [SerializeField] private LocalizationData localizationData; 


    private SheetsService GetSheetsService()
    {
        try
        {
            var credential = GoogleCredential.FromFile(credentialsPath)
                .CreateScoped(SheetsService.Scope.SpreadsheetsReadonly);

            return new SheetsService(new BaseClientService.Initializer()
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
            string range = $"{sheetName}!A1:Z";
            var service = GetSheetsService();

            if (service == null)
            {
                Debug.LogError("Failed to initialize Google Sheets service.");
                return;
            }

            SpreadsheetsResource.ValuesResource.GetRequest request =
                service.Spreadsheets.Values.Get(sheetId, range);

            ValueRange response = request.Execute();
            IList<IList<object>> values = response.Values;

            if (values != null && values.Count > 0)
            {
                List<string[]> data = new List<string[]>();
                foreach (var row in values)
                {
                    data.Add(row.Select(cell => cell?.ToString() ?? "").ToArray());
                }

                if (localizationData != null)
                {
                    localizationData.SetData(data);
                    Debug.Log("Localization data updated via Google Sheets API!");
#if UNITY_EDITOR
                    EditorUtility.SetDirty(localizationData);
                    AssetDatabase.SaveAssets();  // Сохраняем изменения в ScriptableObject
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