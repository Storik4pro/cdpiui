using CDPI_UI.Helper.Items;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace CDPI_UI.Helper.CreateConfigHelper
{
    public partial class CreateConfigPageHelper
    {
        private const string UserCreatedConfigMeta = "UC:v1.0";

        public static ConfigItem CreateConfigItem(
            string packId, 
            string name, 
            string componentId, 
            Dictionary<string, bool> jparams, 
            List<string> vars,
            Dictionary<string, string> commaVars,
            List<AvailableVarValues> availableCommaVarValues,
            string startupString)
        {
            ConfigItem configItem = new()
            {
                meta = UserCreatedConfigMeta,
                packId = packId,
                not_converted_name = name,
                target = [componentId, DatabaseHelper.Instance.GetItemById(componentId)?.CurrentVersion?? "%CURRENT%"],
                jparams = jparams,
                variables = vars,
                commaVars = commaVars,
                availableCommaVarsValues = availableCommaVarValues,
                startup_string = startupString
            };

            return configItem;
        }

        public static bool IsNameCorrect(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return false;

            bool flag = true;

            foreach (char c in input)
            {
                if (!((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z')))
                {
                    flag = false;
                    break;
                }
            }
            return flag;
        }

        public class PrettyLookTextResultModel
        {
            public string ListDirectory = string.Empty;
            public string BinDirectory = string.Empty;
            public string ResultText;
        }

        public static PrettyLookTextResultModel ApplyPrettyFilesReplacement(string input)
        {
            PrettyLookTextResultModel model = new();
            try
            {
                model.ListDirectory = ListDirectoryReplaceRegex().Match(input).Groups[1].Value;
                model.BinDirectory = BinDirectoryReplaceRegex().Match(input).Groups[1].Value;

                model.ResultText = input.Replace(model.ListDirectory, "list://").Replace(model.BinDirectory, "bin://");
            }
            catch
            {
                model.ResultText = input;
            }

            return model;
        }
        public static string GetNormalText(string input, string listDirectory, string binDirectory)
        {
            string text = input.Replace("list://", listDirectory).Replace("bin://", binDirectory);
            return text.Replace("\n", " ");
        }

        public static string ApplyWrappingToString(string input)
        {
            string result = input.Replace(" --new", "\n--new");
            result = result.Replace(" -A", "\n-A");
            result = result.Replace(" --auto", "\n--auto");
            return result;
        }

        [GeneratedRegex(@"""(\$GETCURRENTDIR\(\)/List.*?\\).*?""\s")]
        private static partial Regex ListDirectoryReplaceRegex();

        [GeneratedRegex(@"""(\$GETCURRENTDIR\(\)/Bin.*?\\).*?""\s")]
        private static partial Regex BinDirectoryReplaceRegex();
    }
}
