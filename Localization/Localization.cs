using COSML.Modding;
using System.Collections.Generic;
using System;
using System.Linq;
using COSML;
using UnityEngine;
using System.IO;

namespace Localization
{
    public class Localization : Mod, IGlobalSettings<Localization.GlobalData>
    {
        public Localization() : base("Localization") { }
        public override string GetVersion() => "1.0.0";

        private LangData currentLanguage;
        private List<LangData> languages;
        private int modLanguageStartIndex;

        private FileSystemWatcher watcher;

        private void LoadLanguages()
        {
            if (languages != null)
                return;

            languages = new List<LangData>();
            string i18nPath = Path.Combine(Application.streamingAssetsPath, "i18n");
            Directory.CreateDirectory(i18nPath);
            string[] langFiles = Directory.GetFiles(i18nPath, "*.txt");
            foreach (string langFile in langFiles)
            {
                LangData data = LoadLanguage(langFile);
                if (data == null)
                    continue;
                languages.Add(data);
            }

            watcher = new FileSystemWatcher(i18nPath);
            watcher.Filter = "*.txt";
            watcher.NotifyFilter = NotifyFilters.LastWrite;
            watcher.Changed += OnLanguageUpdate;
            watcher.EnableRaisingEvents = true;
        }

        private void OnLanguageUpdate(object sender, FileSystemEventArgs e)
        {
            Info($"Updated language file {Path.GetFileName(e.FullPath)}");
            LangData newData = LoadLanguage(e.FullPath);
            if (newData == null)
                return;

            // hotswap!
            int index = languages.FindIndex(lang => lang.code == newData.code);
            if (index < 0)
                languages.Add(newData);
            else
                languages[index] = newData;

            if (currentLanguage.code == newData.code)
            {
                currentLanguage = newData;
                AbstractPlateform plateform = GameController.GetInstance().GetPlateformController();
                foreach (I18nText text in UnityEngine.Object.FindObjectsOfType<I18nText>())
                {
                    text.Translate(plateform.GetOptions().i18n, plateform.GetI18nPlateformType());
                }
                foreach (OptionsMainMenu obj in UnityEngine.Object.FindObjectsOfType<OptionsMainMenu>())
                {
                    obj.Translate(plateform.GetOptions().i18n, plateform.GetI18nPlateformType());
                }
            }
        }

        private LangData LoadLanguage(string langFile)
        {
            LangData data = null;
            try
            {
                data = new LangData(langFile);
            }
            catch (Exception ex)
            {
                Error($"Failed to load {Path.GetFileName(langFile)}: {ex}");
            }
            return data;
        }

        [Serializable]
        public class GlobalData
        {
            public string languageCode = null;
        }
        public void OnLoadGlobal(GlobalData data)
        {
            LoadLanguages();
            currentLanguage = languages.Find(lang => lang.code == data.languageCode);
            Info($"Initializing language to {currentLanguage}");
        }
        public GlobalData OnSaveGlobal()
        {
            return new GlobalData() { languageCode = currentLanguage?.code };
        }

        public override void Init()
        {
            LoadLanguages();

            On.OptionsMainMenu.Init += OptionsMainMenu_Init;
            On.OptionsMainMenu.SetOptionsValues += OptionsMainMenu_SetOptionsValues;
            On.OptionsMainMenu.OnClic += OptionsMainMenu_OnClic;

            On.I18nEntry.GetTranslation += I18nEntry_GetTranslation;

            Info("Loaded languages [" + String.Join(", ", languages.Select(data => data.code)) + "]");
        }

        private void OptionsMainMenu_Init(On.OptionsMainMenu.orig_Init orig, OptionsMainMenu self)
        {
            orig(self);
            if (currentLanguage != null)
            {
                self.languageSelector.SetCurrentValue(modLanguageStartIndex + languages.FindIndex(lang => lang.code == currentLanguage.code));
            }
        }

        private void OptionsMainMenu_SetOptionsValues(On.OptionsMainMenu.orig_SetOptionsValues orig, OptionsMainMenu self, AbstractOptions options, bool init)
        {
            int previousCurrentValue = self.languageSelector.GetCurrentValue();
            orig(self, options, init);
            string[] values = ReflectionHelper.GetField<MainMenuSelector, string[]>(self.languageSelector, "values");
            modLanguageStartIndex = values.Length;
            ReflectionHelper.SetField<MainMenuSelector, int>(self.languageSelector, "currentValue", previousCurrentValue);
            self.languageSelector.SetValues(Enumerable.Repeat(values[0], modLanguageStartIndex + languages.Count).ToArray(), options.i18n, init);
        }

        private void OptionsMainMenu_OnClic(On.OptionsMainMenu.orig_OnClic orig, OptionsMainMenu self, int buttonId)
        {
            if (buttonId == 0)
            {
                int languageIndex = self.languageSelector.GetCurrentValue();
                currentLanguage = languages.ElementAtOrDefault(languageIndex - modLanguageStartIndex);
                Info($"Updated language to {currentLanguage}");
                SaveGlobalSettings();
            }
            orig(self, buttonId);
        }

        private string I18nEntry_GetTranslation(On.I18nEntry.orig_GetTranslation orig, I18nEntry self, I18nType type)
        {
            if (currentLanguage == null)
                return orig(self, type);
            string translation;
            if (currentLanguage.translations.TryGetValue(self.key, out translation))
                return translation;
            return self.key;
        }
    }

    class LangData
    {
        internal string code;
        internal Dictionary<string, string> translations;
        internal LangData(string langFile)
        {
            translations = new Dictionary<string, string>();
            code = Path.GetFileNameWithoutExtension(langFile);
            foreach (string line in File.ReadLines(langFile))
            {
                if (line.Length == 0)
                    continue;
                string[] split = line.Split(new char[] { '=' }, 2);
                if (split.Length != 2)
                {
                    throw new FormatException($"line \"{line}\" has no = sign");
                }
                string key = split[0];
                string value = split[1];
                translations[key] = value;
            }
        }
        public override string ToString() => $"LangData[{code}]";
    }
}