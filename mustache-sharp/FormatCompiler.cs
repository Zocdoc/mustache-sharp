﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using mustache.Properties;

namespace mustache
{
    /// <summary>
    /// Parses a format string and returns a text generator.
    /// </summary>
    public sealed class FormatCompiler
    {
        private readonly Dictionary<string, TagDefinition> _tagLookup;
        private readonly Dictionary<string, Regex> _regexLookup;
        private readonly MasterTagDefinition _masterDefinition;

        /// <summary>
        /// Initializes a new instance of a FormatCompiler.
        /// </summary>
        public FormatCompiler()
        {
            _tagLookup = new Dictionary<string, TagDefinition>();
            _regexLookup = new Dictionary<string, Regex>();
            _masterDefinition = new MasterTagDefinition();

            IfTagDefinition ifDefinition = new IfTagDefinition();
            _tagLookup.Add(ifDefinition.Name, ifDefinition);
            ElifTagDefinition elifDefinition = new ElifTagDefinition();
            _tagLookup.Add(elifDefinition.Name, elifDefinition);
            ElseTagDefinition elseDefinition = new ElseTagDefinition();
            _tagLookup.Add(elseDefinition.Name, elseDefinition);
            EachTagDefinition eachDefinition = new EachTagDefinition();
            _tagLookup.Add(eachDefinition.Name, eachDefinition);
            WithTagDefinition withDefinition = new WithTagDefinition();
            _tagLookup.Add(withDefinition.Name, withDefinition);
        }

        /// <summary>
        /// Registers the given tag definition with the parser.
        /// </summary>
        /// <param name="definition">The tag definition to register.</param>
        /// <param name="isTopLevel">Specifies whether the tag is immediately in scope.</param>
        public void RegisterTag(TagDefinition definition, bool isTopLevel)
        {
            if (definition == null)
            {
                throw new ArgumentNullException("definition");
            }
            if (_tagLookup.ContainsKey(definition.Name))
            {
                string message = String.Format(Resources.DuplicateTagDefinition, definition.Name);
                throw new ArgumentException(message, "definition");
            }
            _tagLookup.Add(definition.Name, definition);
        }

        /// <summary>
        /// Builds a text generator based on the given format.
        /// </summary>
        /// <param name="format">The format to parse.</param>
        /// <returns>The text generator.</returns>
        public Generator Compile(string format)
        {
            if (format == null)
            {
                throw new ArgumentNullException("format");
            }
            CompoundGenerator generator = new CompoundGenerator(_masterDefinition, new ArgumentCollection());
            Trimmer trimmer = new Trimmer();
            int formatIndex = buildCompoundGenerator(_masterDefinition, generator, trimmer, format, 0);
            string trailing = format.Substring(formatIndex);
            generator.AddStaticGenerators(trimmer.RecordText(trailing, false, false));
            trimmer.Trim();
            return new Generator(generator);
        }

        private Match findNextTag(TagDefinition definition, string format, int formatIndex)
        {
            Regex regex = prepareRegex(definition);
            return regex.Match(format, formatIndex);
        }

        private Regex prepareRegex(TagDefinition definition)
        {
            Regex regex;
            if (!_regexLookup.TryGetValue(definition.Name, out regex))
            {
                List<string> matches = new List<string>();
                matches.Add(getKeyRegex());
                matches.Add(getCommentTagRegex());
                foreach (string closingTag in definition.ClosingTags)
                {
                    matches.Add(getClosingTagRegex(closingTag));
                }
                foreach (TagDefinition globalDefinition in _tagLookup.Values)
                {
                    if (!globalDefinition.IsContextSensitive)
                    {
                        matches.Add(getTagRegex(globalDefinition));
                    }
                }
                foreach (string childTag in definition.ChildTags)
                {
                    TagDefinition childDefinition = _tagLookup[childTag];
                    matches.Add(getTagRegex(childDefinition));
                }
                matches.Add(getUnknownTagRegex());
                string match = "{{(" + String.Join("|", matches) + ")}}";
                regex = new Regex(match, RegexOptions.Compiled);
                _regexLookup.Add(definition.Name, regex);
            }
            return regex;
        }

        private static string getClosingTagRegex(string tagName)
        {
            StringBuilder regexBuilder = new StringBuilder();
            regexBuilder.Append(@"(?<close>(/(?<name>");
            regexBuilder.Append(tagName);
            regexBuilder.Append(@")\s*?))");
            return regexBuilder.ToString();
        }

        private static string getCommentTagRegex()
        {
            return @"(?<comment>#!.*?)";
        }

        private static string getKeyRegex()
        {
            return @"((?<key>" + RegexHelper.CompoundKey + @")(,(?<alignment>(\+|-)?[\d]+))?(:(?<format>.*?))?)";
        }

        private static string getTagRegex(TagDefinition definition)
        {
            StringBuilder regexBuilder = new StringBuilder();
            regexBuilder.Append(@"(?<open>(#(?<name>");
            regexBuilder.Append(definition.Name);
            regexBuilder.Append(@")");
            foreach (TagParameter parameter in definition.Parameters)
            {
                regexBuilder.Append(@"(\s+?");
                regexBuilder.Append(@"(?<argument>");
                regexBuilder.Append(RegexHelper.CompoundKey);
                regexBuilder.Append(@"))");
                if (!parameter.IsRequired)
                {
                    regexBuilder.Append("?");
                }
            }
            regexBuilder.Append(@"\s*?))");
            return regexBuilder.ToString();
        }

        private string getUnknownTagRegex()
        {
            return @"(?<unknown>(#.*?))";
        }

        private int buildCompoundGenerator(
            TagDefinition tagDefinition, 
            CompoundGenerator generator,
            Trimmer trimmer,
            string format, int formatIndex)
        {
            while (true)
            {
                Match match = findNextTag(tagDefinition, format, formatIndex);

                if (!match.Success)
                {
                    if (tagDefinition.ClosingTags.Any())
                    {
                        string message = String.Format(Resources.MissingClosingTag, tagDefinition.Name);
                        throw new FormatException(message);
                    }
                    break;
                }

                string leading = format.Substring(formatIndex, match.Index - formatIndex);

                if (match.Groups["key"].Success)
                {
                    generator.AddStaticGenerators(trimmer.RecordText(leading, true, true));
                    formatIndex = match.Index + match.Length;
                    string key = match.Groups["key"].Value;
                    string alignment = match.Groups["alignment"].Value;
                    string formatting = match.Groups["format"].Value;
                    KeyGenerator keyGenerator = new KeyGenerator(key, alignment, formatting);
                    generator.AddGenerator(keyGenerator);
                }
                else if (match.Groups["open"].Success)
                {
                    formatIndex = match.Index + match.Length;
                    string tagName = match.Groups["name"].Value;
                    TagDefinition nextDefinition = _tagLookup[tagName];
                    if (nextDefinition == null)
                    {
                        string message = String.Format(Resources.UnknownTag, tagName);
                        throw new FormatException(message);
                    }
                    if (nextDefinition.HasContent)
                    {
                        generator.AddStaticGenerators(trimmer.RecordText(leading, true, false));
                        ArgumentCollection arguments = getArguments(nextDefinition, match);
                        CompoundGenerator compoundGenerator = new CompoundGenerator(nextDefinition, arguments);
                        formatIndex = buildCompoundGenerator(nextDefinition, compoundGenerator, trimmer, format, formatIndex);
                        generator.AddGenerator(nextDefinition, compoundGenerator);
                    }
                    else
                    {
                        generator.AddStaticGenerators(trimmer.RecordText(leading, true, true));
                        Match nextMatch = findNextTag(nextDefinition, format, formatIndex);
                        ArgumentCollection arguments = getArguments(nextDefinition, nextMatch);
                        InlineGenerator inlineGenerator = new InlineGenerator(nextDefinition, arguments);
                        generator.AddGenerator(inlineGenerator);
                    }
                }
                else if (match.Groups["close"].Success)
                {
                    generator.AddStaticGenerators(trimmer.RecordText(leading, true, false));
                    string tagName = match.Groups["name"].Value;
                    TagDefinition nextDefinition = _tagLookup[tagName];
                    formatIndex = match.Index;
                    if (tagName == tagDefinition.Name)
                    {
                        formatIndex += match.Length;
                    }
                    break;
                }
                else if (match.Groups["comment"].Success)
                {
                    generator.AddStaticGenerators(trimmer.RecordText(leading, true, false));
                    formatIndex = match.Index + match.Length;
                }
                else if (match.Groups["unknown"].Success)
                {
                    throw new FormatException(Resources.UnknownTag);
                }
            }
            return formatIndex;
        }

        private static ArgumentCollection getArguments(TagDefinition definition, Match match)
        {
            ArgumentCollection collection = new ArgumentCollection();
            List<Capture> captures = match.Groups["argument"].Captures.Cast<Capture>().ToList();
            List<TagParameter> parameters = definition.Parameters.ToList();
            if (captures.Count > parameters.Count)
            {
                string message = String.Format(Resources.WrongNumberOfArguments, definition.Name);
                throw new FormatException(message);
            }
            if (captures.Count < parameters.Count)
            {
                captures.AddRange(Enumerable.Repeat((Capture)null, parameters.Count - captures.Count));
            }
            foreach (var pair in parameters.Zip(captures, (p, c) => new { Capture = c, Parameter = p }))
            {
                if (pair.Capture == null)
                {
                    if (pair.Parameter.IsRequired)
                    {
                        string message = String.Format(Resources.WrongNumberOfArguments, definition.Name);
                        throw new FormatException(message);
                    }
                    collection.AddArgument(pair.Parameter, null);
                }
                else
                {
                    collection.AddArgument(pair.Parameter, pair.Capture.Value);
                }                
            }
            return collection;
        }
    }
}
