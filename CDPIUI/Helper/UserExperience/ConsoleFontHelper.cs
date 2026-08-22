using CDPIUI.Controls.Dialogs;
using CDPIUI.Core;
using CDPIUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing.Interop;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using WinUI3Localizer;

namespace CDPIUI.Helper.UserExperience
{
    public partial class ConsoleFontHelper : INotifyPropertyChanged
    {
        public static ConsoleFontHelper Instance { get; } = new();

        #region Settings

        private FontFamily fontFamily;
        public FontFamily FontFamily 
        {
            get => fontFamily;
            set
            {
                if (ReferenceEquals(fontFamily, value))
                    return;

                fontFamily = value;

                SettingsManager.Instance.SetValue<string>("PSEUDOCONSOLE", "fontFamily", fontFamily.Source.ToString());
                Notify(nameof(FontFamily));
            }
        }

        private double fontSize;
        public double FontSize
        {
            get => fontSize;
            set
            {
                if (Equals(fontSize, value))
                    return;

                fontSize = value;

                SettingsManager.Instance.SetValue<double>("PSEUDOCONSOLE", "fontSize", fontSize);
                Notify(nameof(FontSize));
            }
        }

        #endregion

        #region UI

        private bool onlyMonospace = SettingsManager.Instance.GetValueOrDefault<bool>("PSEUDOCONSOLE", "showOnlyMonospaceFontsInView", defaultValue:true);
        public bool OnlyMonospace
        {
            get => onlyMonospace;
            set
            {
                if (Equals(onlyMonospace, value))
                    return;

                SettingsManager.Instance.SetValue<bool>("PSEUDOCONSOLE", "showOnlyMonospaceFontsInView", onlyMonospace);
                onlyMonospace = value;
                LoadFonts();
                Notify(nameof(OnlyMonospace));
            }
        }

        private readonly ObservableCollection<FontFamily> availableFonts = [];
        public ObservableCollection<FontFamily> AvailableFonts
        {
            get
            {
                if (availableFonts is null || availableFonts.Count == 0)
                {
                    LoadFonts();
                    return availableFonts;
                }
                return availableFonts;
            }
        }

        public FontFamily SelectedFontFamilyInCollection
        {
            get => AvailableFonts.FirstOrDefault(x => x.Source == FontFamily.Source);
        }

        public List<double> FontSizes = [8, 9, 10, 11, 12, 14, 16, 18, 20, 24, 28, 36, 48, 72];

        #endregion

        public event PropertyChangedEventHandler PropertyChanged;

        public async void ShowFontSettingsDialogForXamlRoot(XamlRoot xamlRoot)
        {
            FontSettingsContentDialog dialog = new() { XamlRoot = xamlRoot };

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                FontFamily = dialog.FontName;
                FontSize = dialog.FontSize;
            }
        }

        private ConsoleFontHelper()
        {
            FontFamily = GetFont(SettingsManager.Instance.GetValue<string>("PSEUDOCONSOLE", "fontFamily"));
            FontSize = SettingsManager.Instance.GetValue<double>("PSEUDOCONSOLE", "fontSize");
        }

        

        private static FontFamily GetFont(string fontFamily)
        {
            try
            {
                return new FontFamily(fontFamily);
            }
            catch
            {
                return new FontFamily("Consolas");
            }
        }

        private void LoadFonts()
        {
            availableFonts.Clear();

            foreach (System.Drawing.FontFamily font in System.Drawing.FontFamily.Families)
            {
                if (!OnlyMonospace)
                {
                    availableFonts.Add(new FontFamily(font.Name));
                    continue;
                }

                if (font.IsStyleAvailable(System.Drawing.FontStyle.Regular))
                {
                    float diff;
                    using (System.Drawing.Font _font = new System.Drawing.Font(font, 16))
                    {
                        diff = TextRenderer.MeasureText("WWW", _font).Width - TextRenderer.MeasureText("...", _font).Width;
                    }
                    if (Math.Abs(diff) < float.Epsilon * 2)
                    {
                        availableFonts.Add(new FontFamily(font.Name));
                    }
                }
                
            }

            Notify(nameof(AvailableFonts));
            Notify(nameof(SelectedFontFamilyInCollection));
        }

        private void Notify(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
