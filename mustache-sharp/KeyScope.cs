﻿using System;
using System.Collections.Generic;
using System.Globalization;
using mustache.Properties;

namespace mustache
{
    /// <summary>
    /// Represents a scope of keys.
    /// </summary>
    public sealed class KeyScope
    {
        private readonly object _source;
        private readonly KeyScope _parent;

        /// <summary>
        /// Initializes a new instance of a KeyScope.
        /// </summary>
        /// <param name="source">The object to search for keys in.</param>
        internal KeyScope(object source)
            : this(source, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of a KeyScope.
        /// </summary>
        /// <param name="source">The object to search for keys in.</param>
        /// <param name="parent">The parent scope to search in if the value is not found.</param>
        internal KeyScope(object source, KeyScope parent)
        {
            _parent = parent;
            _source = source;
        }

        /// <summary>
        /// Creates a child scope that searches for keys in the given object.
        /// </summary>
        /// <param name="source">The object to search for keys in.</param>
        /// <returns>The new child scope.</returns>
        public KeyScope CreateChildScope(object source)
        {
            KeyScope scope = new KeyScope(source, this);
            return scope;
        }

        /// <summary>
        /// Attempts to find the value associated with the key with given name.
        /// </summary>
        /// <param name="name">The name of the key.</param>
        /// <returns>The value associated with the key with the given name.</returns>
        /// <exception cref="System.Collections.Generic.KeyNotFoundException">A key with the given name could not be found.</exception>
        internal object Find(string name)
        {
            string[] names = name.Split('.');
            string member = names[0];
            object nextLevel = _source;
            if (member != "this")
            {
                nextLevel = find(member);
            }
            for (int index = 1; index < names.Length; ++index)
            {
                IDictionary<string, object> context = toLookup(nextLevel);
                member = names[index];
                nextLevel = context[member];
            }
            return nextLevel;
        }

        private object find(string name)
        {
            IDictionary<string, object> lookup = toLookup(_source);
            if (lookup.ContainsKey(name))
            {
                return lookup[name];
            }
            if (_parent == null)
            {
                string message = String.Format(CultureInfo.CurrentCulture, Resources.KeyNotFound, name);
                throw new KeyNotFoundException(message);
            }
            return _parent.find(name);
        }

        private static IDictionary<string, object> toLookup(object value)
        {
            IDictionary<string, object> lookup = value as IDictionary<string, object>;
            if (lookup == null)
            {
                lookup = new PropertyDictionary(value);
            }
            return lookup;
        }
    }
}
