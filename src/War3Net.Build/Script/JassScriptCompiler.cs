﻿// ------------------------------------------------------------------------------
// <copyright file="JassScriptCompiler.cs" company="Drake53">
// Licensed under the MIT license.
// See the LICENSE file in the project root for more information.
// </copyright>
// ------------------------------------------------------------------------------

using System.Diagnostics;
using System.IO;
using System.Text;

using War3Net.Build.Providers;
using War3Net.CodeAnalysis.Jass;
using War3Net.CodeAnalysis.Jass.Renderer;
using War3Net.CodeAnalysis.Jass.Syntax;

namespace War3Net.Build.Script
{
    internal sealed class JassScriptCompiler : ScriptCompiler
    {
        private readonly string _jasshelperPath;
        private readonly string _commonPath;
        private readonly string _blizzardPath;

        private readonly JassRendererOptions _rendererOptions;

        public JassScriptCompiler(ScriptCompilerOptions options, JassRendererOptions rendererOptions)
            : base(options)
        {
            if (options.SourceDirectory != null)
            {
                // todo: retrieve these vals from somewhere
                var x86 = true;
                var ptr = false;

                _jasshelperPath = Path.Combine(new FileInfo(WarcraftPathProvider.GetExePath(x86, ptr)).DirectoryName, "JassHelper", "jasshelper.exe");

                var jasshelperDocuments = Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments),
                ptr ? "Warcraft III Public Test" : "Warcraft III",
                "Jasshelper");
                _commonPath = Path.Combine(jasshelperDocuments, "common.j");
                _blizzardPath = Path.Combine(jasshelperDocuments, "Blizzard.j");
            }

            _rendererOptions = rendererOptions;
        }

        public override void BuildMainAndConfig(out string mainFunctionFilePath, out string configFunctionFilePath)
        {
            var functionBuilderData = new FunctionBuilderData(Options.MapInfo, Options.MapDoodads, Options.MapUnits, Options.LobbyMusic, false);
            var functionBuilder = new JassFunctionBuilder(functionBuilderData);

            mainFunctionFilePath = Path.Combine(Options.OutputDirectory, "main.j");
            RenderFunctionSyntaxToFile(functionBuilder.BuildMainFunction(), mainFunctionFilePath);

            configFunctionFilePath = Path.Combine(Options.OutputDirectory, "config.j");
            RenderFunctionSyntaxToFile(functionBuilder.BuildConfigFunction(), configFunctionFilePath);
        }

        public override bool Compile(out string scriptFilePath, params string[] additionalSourceFiles)
        {
            var inputScript = Path.Combine(Options.OutputDirectory, "files.j");
            using (var inputScriptStream = FileProvider.OpenNewWrite(inputScript))
            {
                using (var streamWriter = new StreamWriter(inputScriptStream))
                {
                    foreach (var file in Directory.EnumerateFiles(Options.SourceDirectory, "*.j", SearchOption.AllDirectories))
                    {
                        streamWriter.WriteLine($"//! import \"{file}\"");
                    }

                    /*foreach (var reference in references)
                    {
                        foreach (var file in reference.EnumerateFiles("*.j", SearchOption.AllDirectories, null))
                        {
                            streamWriter.WriteLine($"//! import \"{file}\"");
                        }
                    }*/

                    foreach (var file in additionalSourceFiles)
                    {
                        streamWriter.WriteLine($"//! import \"{file}\"");
                    }
                }
            }

            var outputScript = "war3map.j";
            scriptFilePath = Path.Combine(Options.OutputDirectory, outputScript);
            var jasshelperOutputScript = Path.Combine(Options.OutputDirectory, Options.Obfuscate ? "war3map.original.j" : outputScript);
            var jasshelperOptions = Options.Debug ? "--debug" : Options.Optimize ? string.Empty : "--nooptimize";
            var jasshelper = Process.Start(_jasshelperPath, $"{jasshelperOptions} --scriptonly \"{_commonPath}\" \"{_blizzardPath}\" \"{inputScript}\" \"{jasshelperOutputScript}\"");
            jasshelper.WaitForExit();

            var success = jasshelper.ExitCode == 0;
            if (success && Options.Obfuscate)
            {
                JassObfuscator.Obfuscate(jasshelperOutputScript, scriptFilePath, _commonPath, _blizzardPath);
            }

            return success;
        }

        public override void CompileSimple(out string scriptFilePath, params string[] additionalSourceFiles)
        {
            scriptFilePath = Path.Combine(Options.OutputDirectory, "war3map.j");
            using (var fileStream = FileProvider.OpenNewWrite(scriptFilePath))
            {
                using (var writer = new StreamWriter(fileStream, new UTF8Encoding(false, true), 1024, true))
                {
                    foreach (var additionalSourceFile in additionalSourceFiles)
                    {
                        writer.Write(File.ReadAllText(additionalSourceFile));
                        writer.WriteLine();
                    }
                }
            }
        }

        private void RenderFunctionSyntaxToFile(FunctionSyntax function, string path)
        {
            using (var fileStream = FileProvider.OpenNewWrite(path))
            {
                using (var writer = new StreamWriter(fileStream, new UTF8Encoding(false, true)))
                {
                    var renderer = new JassRenderer(writer);
                    renderer.Options = _rendererOptions;
                    renderer.Render(JassSyntaxFactory.File(function));
                }
            }
        }
    }
}