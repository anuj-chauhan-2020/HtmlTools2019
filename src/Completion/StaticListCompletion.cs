﻿using Microsoft.WebTools.Languages.Html.Editor.Completion;
using Microsoft.WebTools.Languages.Html.Editor.Completion.Def;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace HtmlTools
{
    public abstract class StaticListCompletion : IHtmlCompletionListProvider
    {
        private readonly IReadOnlyDictionary<string, IEnumerable<string>> values;
        private static readonly ReadOnlyCollection<HtmlCompletion> _empty = new ReadOnlyCollection<HtmlCompletion>(new HtmlCompletion[0]);

        protected static ReadOnlyCollection<HtmlCompletion> Empty => _empty;

        public string CompletionType => CompletionTypes.Values;
        ///<summary>Gets the property that serves as a key to look up completion values against.</summary>
        protected abstract string KeyProperty { get; }

        protected StaticListCompletion(Dictionary<string, IEnumerable<string>> values)
        {
            this.values = values;
        }

        ///<summary>Creates a collection of HTML completion items from a list of static values.</summary>
        protected static IEnumerable<string> Values(params string[] staticValues)
        {
            return staticValues;
        }

        ///<summary>Creates a collection of HTML completion items from a list of static values.</summary>
        protected static IEnumerable<string> Values(IEnumerable<string> staticValues)
        {
            return staticValues;
        }

        public virtual IList<HtmlCompletion> GetEntries(HtmlCompletionContext context)
        {
            Microsoft.WebTools.Languages.Html.Tree.Nodes.AttributeNode attr = context.Element.GetAttribute(KeyProperty);

            if (attr == null)
            {
                return Empty;
            }

            if (values.TryGetValue(attr.Value, out IEnumerable<string> result))
            {
                return result.Select(s => new SimpleHtmlCompletion(s, context.Session)).ToList<HtmlCompletion>();
            }

            return Empty;
        }
    }
}