using CDPIUI.AddOns.ConfigImport;
using CDPIUI.Core.ComponentServices.Helpers.Configuration;
using CDPIUI.Shared.Models;
using CDPIUI.Shared.PrettyErrorConvertionService;
using CDPIUI.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WinUI3Localizer;

namespace CDPIUI.Helper.AddOns.ConfigImport
{
    internal class ConfigImportHelper
    {
        /// <summary>
        /// Request <see cref="System.Windows.Forms.OpenFileDialog"/> to select single config file, than import it.
        /// </summary>
        /// <param name="requestedTarget">Default requested target</param>
        /// <returns><see cref="OperationResultModel{ConfigImportResult}"/> (allways not failure) with <see cref="ConfigImportResult"/>.
        /// You must check Success of main model and IsSuccessful of result model.
        /// </returns>
        public static OperationResultModel<ConfigImportResult> ImportConfigFromFile(string requestedTarget = null)
        {
            var result = OpenFileSelectionDialog(false);
            if (!result.Success) return OperationResultModel<ConfigImportResult>.UnSuccessResult();

            ConfigImportService service = new();
            return OperationResultModel<ConfigImportResult>.SuccessResult(service.Import(result.Result.First(), requestedTarget: requestedTarget));
        }

        /// <summary>
        /// Request <see cref="System.Windows.Forms.OpenFileDialog"/> to select single config file and return <see cref="string[]"/> of 
        /// selected files.
        /// </summary>
        /// <param name="multiselect">Enable multiselect</param>
        /// <returns><see cref="OperationResultModel{string[]}"/> with list of files user selected.</returns>
        public static OperationResultModel<string[]> OpenFileSelectionDialog(bool multiselect)
        {
            ILocalizer localizer = Localizer.Get();

            string[] filePaths;
            using (System.Windows.Forms.OpenFileDialog openFileDialog = new())
            {
                openFileDialog.Title = localizer.GetLocalizedString("ImportConfig");
                openFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

                openFileDialog.Multiselect = multiselect;

                openFileDialog.Filter =
                    $"{localizer.GetLocalizedString("TextFiles")} (*.txt)|*.txt|" +
                    $"{localizer.GetLocalizedString("BatchFiles")} (*.bat;*.cmd)|*.bat;*.cmd|" +
                    $"{localizer.GetLocalizedString("JsonFiles")} (*.json)|*.json|" +
                    $"{localizer.GetLocalizedString("AllSupported")} (*.txt;*.bat;*.cmd;*.json)|*.txt;*.bat;*.cmd;*.json";
                openFileDialog.RestoreDirectory = true;

                openFileDialog.FilterIndex = 4;

                if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    filePaths = openFileDialog.FileNames;
                }
                else
                {
                    return OperationResultModel<string[]>.UnSuccessResult();
                }
            }
            return OperationResultModel<string[]>.SuccessResult(filePaths);
        }
    }
}
