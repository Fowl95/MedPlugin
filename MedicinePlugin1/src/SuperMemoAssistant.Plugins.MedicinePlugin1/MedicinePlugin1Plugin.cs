#region License & Metadata

// The MIT License (MIT)
// 
// Permission is hereby granted, free of charge, to any person obtaining a
// copy of this software and associated documentation files (the "Software"),
// to deal in the Software without restriction, including without limitation
// the rights to use, copy, modify, merge, publish, distribute, sublicense,
// and/or sell copies of the Software, and to permit persons to whom the 
// Software is furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.
// 
// 
// Created On:   7/18/2020 1:23:41 AM
// Modified By:  jacop

#endregion




using System;
using System.Collections.Generic;
using System.Runtime.Remoting;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using SuperMemoAssistant.Extensions;
using SuperMemoAssistant.Interop.SuperMemo.Content.Contents;
using SuperMemoAssistant.Interop.SuperMemo.Content.Models;
using SuperMemoAssistant.Interop.SuperMemo.Elements.Builders;
using SuperMemoAssistant.Interop.SuperMemo.Elements.Models;
using SuperMemoAssistant.Services;
using SuperMemoAssistant.Services.IO.Keyboard;
using SuperMemoAssistant.Sys.IO.Devices;

namespace SuperMemoAssistant.Plugins.MedicinePlugin1
{
  using System.Diagnostics.CodeAnalysis;
  using SuperMemoAssistant.Services.Sentry;

  // ReSharper disable once UnusedMember.Global
  // ReSharper disable once ClassNeverInstantiated.Global
  [SuppressMessage("Microsoft.Naming", "CA1724:TypeNamesShouldNotMatchNamespaces")]
  public class MedicinePlugin1Plugin : SentrySMAPluginBase<MedicinePlugin1Plugin>
  {
    private const string DelimiterContent  = @"//";
    private const string PatternCloze      = @"<U>([^\&]*)</U>";
    private const string DelimiterSections = @"(?:&lt;|\<)hr(?:&gt;|\>)";
    private const string ReplaceWithCloze  = @"[...]";
    



    #region Constructors

    /// <inheritdoc />
    public MedicinePlugin1Plugin() : base("https://b411da802c89489d979c17c6705f940a@o421996.ingest.sentry.io/5342669") { }

    #endregion




    #region Properties Impl - Public

    /// <inheritdoc />
    public override string Name => "MedicinePlugin1";

    /// <inheritdoc />
    public override bool HasSettings => false;

    #endregion




    #region Methods Impl

    /// <inheritdoc />
    protected override void PluginInit()
    {
      Svc.HotKeyManager.RegisterGlobal(
        "TopicToClozes",
        "TopicToClozes",
        HotKeyScopes.SM,
        new HotKey(Key.S, KeyModifiers.CtrlAlt),
        TopicToClozes
      );
      // Insert code that needs to be run when the plugin is initialized.
      // Typical initialization code consists of:
      // - Registering keyboard hotkeys
      // - Registering to be notified about events (e.g. OnElementChanged)
      // - Initializing your own services
      // - Publishing services for other plugins

      // If you have questions or issues, you can:
      // - Check our wiki for developer guides https://sma.supermemo.wiki/
      // - Browse through our plugins' source code https://github.com/supermemo/
      // - Ask for help on our Discord server https://discord.gg/vUQhqCT

      // Uncomment to register an event handler which will be notified when the displayed element changes
      // Svc.SM.UI.ElementWdw.OnElementChanged += new ActionProxy<SMDisplayedElementChangedEventArgs>(OnElementChanged);
    }

    // Set HasSettings to true, and uncomment this method to add your custom logic for settings
    // /// <inheritdoc />
    // public override void ShowSettings()
    // {
    // }

    #endregion




    #region Methods

    private void TopicToClozes()
    {
      try
      {
        var text = GetCurrentElementContent();
        var sectionArray = Regex.Split(text, DelimiterSections, RegexOptions.IgnoreCase);

        var storedTitles = new List<string>();
        Match matchedHeader;
        foreach (string sectionString in sectionArray)
        {

          matchedHeader = FindHeader(storedTitles.Count + 1, sectionString, out int matchedLevel);
          var sectionTitle = matchedHeader.Groups[1].Value;

          var currentTitleLevel = matchedLevel;

          UpdateLevels(storedTitles, currentTitleLevel, sectionTitle);

          var contentArray = Regex.Split(sectionString, DelimiterContent, RegexOptions.IgnoreCase); 
          //String[] contentArray = sectionString.Split(new string[] { DelimiterContent }, StringSplitOptions.None);

          foreach (var contentToCloze in contentArray)
          {

            var clozedContent = Regex.Replace(contentToCloze, PatternCloze, "[...]", RegexOptions.IgnoreCase);
            var rgxCloze = Regex.Match(contentToCloze, PatternCloze);
            string combinedTitles = string.Join(" : ", storedTitles.ToArray());
            string newLine = Environment.NewLine;
            var fullContent = "<STRONG>" + combinedTitles + "</STRONG>" + " :" + "<p>" + newLine + clozedContent;

            var questionContent = new TextContent(false, fullContent);
            var answerContent   = new TextContent(false, rgxCloze.Groups[1].Value, AtFlags.NonQuestion);

            var elemBuilder = new ElementBuilder(
              ElementType.Item,
              questionContent,
              answerContent);

            elemBuilder.WithParent(Svc.SM.UI.ElementWdw.CurrentElement);

            bool success = Svc.SM.Registry.Element.Add(
              out var results,
              ElemCreationFlags.ForceCreate,
              elemBuilder);

            if (success == false)
            {
              var errMsg = results.GetErrorString();

              MessageBox.Show(errMsg);
            }

            /*Console.WriteLine("Q: {0} : {1}", combinedTitles, clozedContent);
            Console.WriteLine("A: {0}", rgxCloze.Groups[1].Value);
            */


          }
        }

      }
      catch (RemotingException) { }

      static Regex GetTitleRegex(int level)
      {
        return new Regex(@$"(?:&lt;|\<)h{level}(?:&gt;|\>)([^\&]*)(?:&lt;|\<)/h{level}(?:&gt;|\>)", RegexOptions.IgnoreCase);
      } 


      // Remove all elements from "titleLevels" until we have cleared all items with a level higher or equal to "newLevel"
      // Add the new item using "newTitle"
      static void UpdateLevels(List<string> titleLevels, int newLevel, string newTitle)
      {
        while (titleLevels.Count >= newLevel)
        {
          titleLevels.RemoveAt(titleLevels.Count - 1);
        }

        titleLevels.Add(newTitle);
      }

      // Try to match "maxHeaderLevel", then "maxHeaderLevel - 1" if it didn't match, then "maxHeaderLevel - 2", etc.
      // Set matchedLevel to the actual level that is found (if any)
      // Return Match (if any)

      static Match FindHeader(int maxHeaderLevel, string sectionText, out int matchedLevel)
      {

        var   headerRegex  = GetTitleRegex(maxHeaderLevel);
        var   headerMatch  = headerRegex.Match(sectionText);
        Match sectionTitle = headerRegex.Match(String.Empty);
        matchedLevel = 1;
        if (headerMatch.Success)
        {
          matchedLevel = maxHeaderLevel;
          sectionTitle = headerMatch;
        }

        for (int levelToFind = maxHeaderLevel; levelToFind > 0 && headerMatch.Success == false; levelToFind--)
        {
          headerRegex = GetTitleRegex(levelToFind);
          headerMatch = headerRegex.Match(sectionText);

          if (headerMatch.Success)
          {
            matchedLevel = levelToFind;
            sectionTitle = headerMatch;
          }
        }

        return sectionTitle;
      }

      static string GetCurrentElementContent()
      {
        var ctrlGroup = Svc.SM.UI.ElementWdw.ControlGroup;
        var htmlCtrl  = ctrlGroup?.GetFirstHtmlControl()?.AsHtml();
        return htmlCtrl?.Text;
      }
      // Uncomment to register an event handler for element changed events
      // [LogToErrorOnException]
      // public void OnElementChanged(SMDisplayedElementChangedEventArgs e)
      // {
      //   try
      //   {
      //     Insert your logic here
      //   }
      //   catch (RemotingException) { }
      // }

      #endregion
    }
  }
}
