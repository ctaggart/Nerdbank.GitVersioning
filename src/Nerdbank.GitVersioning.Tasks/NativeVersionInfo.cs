﻿namespace Nerdbank.GitVersioning.Tasks
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Text;
    using Microsoft.Build.Framework;
    using Microsoft.Build.Utilities;

    public class NativeVersionInfo : Task
    {
        private const int VFT_APP = 0x1;
        private const int VFT_DLL = 0x2;

        private const string FileHeaderComment = @"------------------------------------------------------------------------------
 <auto-generated>
     This code was generated by a tool.
     Runtime Version:4.0.30319.42000

     Changes to this file may cause incorrect behavior and will be lost if
     the code is regenerated.
 </auto-generated>
------------------------------------------------------------------------------
";

        private const string VersionStringDefineContent = @"#if defined(_UNICODE)
#define NBGV_VERSION_STRING(x) L ##x
#else
#define NBGV_VERSION_STRING(x) x
#endif";

        private const string VersionInfoContent = @"#ifdef RC_INVOKED

#include <winres.h>

VS_VERSION_INFO VERSIONINFO
  FILEVERSION     NBGV_FILE_MAJOR_VERSION,NBGV_FILE_MINOR_VERSION,NBGV_FILE_BUILD_VERSION,NBGV_FILE_REVISION_VERSION
  PRODUCTVERSION  NBGV_PRODUCT_MAJOR_VERSION,NBGV_PRODUCT_MINOR_VERSION,NBGV_PRODUCT_BUILD_VERSION,NBGV_PRODUCT_REVISION_VERSION
  FILEFLAGSMASK   0x3FL
#ifdef _DEBUG
  FILEFLAGS       0x1L
#else
  FILEFLAGS       0x0L
#endif
  FILEOS          0x4L
  FILETYPE        NBGV_FILE_TYPE
  FILESUBTYPE     0x0L
BEGIN
  BLOCK ""StringFileInfo""
  BEGIN
    BLOCK NBGV_VERSION_BLOCK
    BEGIN
      VALUE ""CompanyName"", NGBV_COMPANY
      VALUE ""FileDescription"", NGBV_TITLE
      VALUE ""FileVersion"", NBGV_FILE_VERSION
      VALUE ""InternalName"", NGBV_INTERNAL_NAME
      VALUE ""OriginalFilename"", NGBV_FILE_NAME
      VALUE ""ProductName"", NGBV_PRODUCT
      VALUE ""ProductVersion"", NBGV_INFORMATIONAL_VERSION
      VALUE ""LegalCopyright"", NBGV_COPYRIGHT
    END
  END

  BLOCK ""VarFileInfo""
  BEGIN
    VALUE ""Translation"", NBGV_LCID, NBGV_CODEPAGE
  END
END

#endif";

        private CodeGenerator generator;

        [Required]
        public string OutputFile { get; set; }

        [Required]
        public string CodeLanguage { get; set; }

        [Required]
        public string ConfigurationType { get; set; }

        public string AssemblyName { get; set; }

        public string AssemblyVersion { get; set; }

        public string AssemblyFileVersion { get; set; }

        public string AssemblyInformationalVersion { get; set; }

        public string AssemblyTitle { get; set; }

        public string AssemblyProduct { get; set; }

        public string AssemblyCopyright { get; set; }

        public string AssemblyCompany { get; set; }

        public string AssemblyLanguage { get; set; }

        public string AssemblyCodepage { get; set; }

        public string TargetFileName { get; set; }

        public override bool Execute()
        {
            this.generator = this.CreateGenerator();
            if (this.generator != null)
            {
                this.generator.StartFile();

                this.generator.AddComment(FileHeaderComment);
                this.generator.AddBlankLine();

                this.generator.AddContent(VersionStringDefineContent);
                this.generator.AddBlankLine();

                this.CreateDefines();
                this.generator.AddBlankLine();

                this.CreateVersionInfo();

                this.generator.EndFile();

                Directory.CreateDirectory(Path.GetDirectoryName(this.OutputFile));
                File.WriteAllText(this.OutputFile, this.generator.GetCode());
            }

            return !this.Log.HasLoggedErrors;
        }

        private void CreateDefines()
        {
            var fileType = 0;

            switch (this.ConfigurationType.ToUpperInvariant())
            {
                case "APPLICATION":
                    fileType = VFT_APP;
                    break;

                case "DYNAMICLIBRARY":
                    fileType = VFT_DLL;
                    break;

                default:
                    this.Log.LogError("Unsupported ConfigurationType '{0}'. Only 'Application' and 'DynamicLibrary' are supported at this time.", this.ConfigurationType);
                    return;
            }

            if (!Version.TryParse(this.AssemblyFileVersion, out var fileVersion))
            {
                this.Log.LogError("Cannot process AssemblyFileVersion '{0}' into a valid four part version.", this.AssemblyFileVersion);
                return;
            }

            if (!Version.TryParse(this.AssemblyVersion, out var productVersion))
            {
                productVersion = fileVersion;
            }

            var lcid = 0;

            if (!string.IsNullOrWhiteSpace(this.AssemblyLanguage))
            {
                if (!int.TryParse(this.AssemblyLanguage, out lcid))
                {
#if NET45
                    try
                    {
                        var cultureInfo = new CultureInfo(this.AssemblyLanguage);

                        lcid = cultureInfo.LCID;
                    }
                    catch
                    {
                        this.Log.LogError("Unknown AssemblyLanguage '{0}'. Cannot determine LCID for that culture.", this.AssemblyLanguage);
                        return;
                    }
#else
                    this.Log.LogError("Unknown AssemblyLanguage '{0}'. Must specify the language as an LCID.", this.AssemblyLanguage);
#endif
                }
            }

            if (!int.TryParse(this.AssemblyCodepage, out var codepage))
            {
                codepage = 0;
            }

            var numericFields = new Dictionary<string, int>
                {
                    { "NBGV_FILE_MAJOR_VERSION", fileVersion.Major },
                    { "NBGV_FILE_MINOR_VERSION", fileVersion.Minor },
                    { "NBGV_FILE_BUILD_VERSION", fileVersion.Build },
                    { "NBGV_FILE_REVISION_VERSION", fileVersion.Revision },
                    { "NBGV_PRODUCT_MAJOR_VERSION", productVersion.Major },
                    { "NBGV_PRODUCT_MINOR_VERSION", productVersion.Minor },
                    { "NBGV_PRODUCT_BUILD_VERSION", productVersion.Build },
                    { "NBGV_PRODUCT_REVISION_VERSION", productVersion.Revision },
                    { "NBGV_FILE_TYPE", fileType },
                    { "NBGV_LCID", lcid },
                    { "NBGV_CODEPAGE", codepage },
                };

            var stringFields = new Dictionary<string, string>
                {
                    { "NBGV_PRODUCT_VERSION", productVersion.ToString() },
                    { "NBGV_FILE_VERSION", fileVersion.ToString() },
                    { "NBGV_INFORMATIONAL_VERSION", DefaultIfEmpty(this.AssemblyInformationalVersion, productVersion.ToString()) },
                    { "NGBV_FILE_NAME", this.TargetFileName },
                    { "NGBV_INTERNAL_NAME", Path.GetFileNameWithoutExtension(this.TargetFileName) },
                    { "NGBV_TITLE", DefaultIfEmpty(this.AssemblyTitle, this.AssemblyName) },
                    { "NGBV_PRODUCT", DefaultIfEmpty(this.AssemblyProduct, this.AssemblyName) },
                    { "NBGV_COPYRIGHT", DefaultIfEmpty(this.AssemblyCopyright, $"Copyright (c) {DateTime.Now.Year}. All rights reserved.") },
                    { "NGBV_COMPANY", DefaultIfEmpty(this.AssemblyCompany, this.AssemblyName) },
                    { "NBGV_VERSION_BLOCK", (lcid << 16 | codepage).ToString("X8") },
                };

            foreach (var pair in numericFields)
            {
                this.generator.AddDefine(pair.Key, pair.Value);
            }

            foreach (var pair in stringFields)
            {
                if (!string.IsNullOrWhiteSpace(pair.Value))
                {
                    this.generator.AddDefine(pair.Key, pair.Value);
                }
            }
        }

        private void CreateVersionInfo()
        {
            this.generator.AddContent(VersionInfoContent);
        }

        private CodeGenerator CreateGenerator()
        {
            switch (this.CodeLanguage.ToLowerInvariant())
            {
                case "c++":
                    return new CodeGenerator();
                default:
                    this.Log.LogError("Code provider not available for language: {0}. No version info will be embedded into assembly.", this.CodeLanguage);
                    return null;
            }
        }

        private class CodeGenerator
        {
            protected readonly StringBuilder codeBuilder;

            internal CodeGenerator()
            {
                this.codeBuilder = new StringBuilder();
            }

            internal void AddComment(string comment)
            {
                this.AddCodeComment(comment, "//");
            }

            internal void StartFile()
            {
                this.codeBuilder.AppendLine("#pragma once");
            }

            internal void AddContent(string content)
            {
                this.codeBuilder.AppendLine(content);
            }

            internal void AddDefine(string name, int value)
            {
                this.codeBuilder.AppendLine($"#define {name} {value}");
            }

            internal void AddDefine(string name, string value)
            {
                var escapedValue = value.Replace("\\", "\\\\");

                this.codeBuilder.AppendLine($"#define {name} NBGV_VERSION_STRING(\"{escapedValue}\")");
            }

            internal void EndFile()
            {
            }

            internal string GetCode() => this.codeBuilder.ToString();

            internal void AddBlankLine()
            {
                this.codeBuilder.AppendLine();
            }

            protected void AddCodeComment(string comment, string token)
            {
                var sr = new StringReader(comment);
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    this.codeBuilder.Append(token);
                    this.codeBuilder.AppendLine(line);
                }
            }
        }

        private static string DefaultIfEmpty(string value, string defaultValue)
        {
            return string.IsNullOrWhiteSpace(value) ? defaultValue : value;
        }
    }
}
