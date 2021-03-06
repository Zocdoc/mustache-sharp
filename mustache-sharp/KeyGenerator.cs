﻿using System;
using System.IO;
using System.Text;

namespace mustache
{
    /// <summary>
    /// Substitutes a key placeholder with the textual representation of the associated object.
    /// </summary>
    internal sealed class KeyGenerator : IGenerator
    {
        private readonly string _key;
        private readonly string _format;

        /// <summary>
        /// Initializes a new instance of a KeyGenerator.
        /// </summary>
        /// <param name="key">The key to substitute with its value.</param>
        /// <param name="alignment">The alignment specifier.</param>
        /// <param name="formatting">The format specifier.</param>
        public KeyGenerator(string key, string alignment, string formatting)
        {
            _key = key;
            _format = getFormat(alignment, formatting);
        }

        private static string getFormat(string alignment, string formatting)
        {
            StringBuilder formatBuilder = new StringBuilder();
            formatBuilder.Append("{0");
            if (!String.IsNullOrWhiteSpace(alignment))
            {
                formatBuilder.Append(",");
                formatBuilder.Append(alignment.TrimStart('+'));
            }
            if (!String.IsNullOrWhiteSpace(formatting))
            {
                formatBuilder.Append(":");
                formatBuilder.Append(formatting);
            }
            formatBuilder.Append("}");
            return formatBuilder.ToString();
        }

        void IGenerator.GetText(KeyScope scope, TextWriter writer)
        {
            object value = scope.Find(_key);
            writer.Write(_format, value);
        }
    }
}
