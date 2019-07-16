﻿using Microsoft.VisualStudio.Utilities;
using Microsoft.WebTools.Languages.Html.Editor.Validation.Def;
using Microsoft.WebTools.Languages.Html.Editor.Validation.Errors;
using Microsoft.WebTools.Languages.Html.Editor.Validation.Validators;
using Microsoft.WebTools.Languages.Html.Tree.Nodes;
using Microsoft.WebTools.Languages.Shared.ContentTypes;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;

namespace HtmlTools
{
    [Export(typeof(IHtmlElementValidatorProvider))]
    [ContentType(HtmlContentTypeDefinition.HtmlContentType)]
    public class RelLogoValidatorProvider : BaseHtmlElementValidatorProvider<RelLogoValidator>
    { }

    public class RelLogoValidator : BaseValidator
    {
        public override IList<IHtmlValidationError> ValidateElement(ElementNode element)
        {
            var results = new ValidationErrorCollection();

            if (element.Name != "link" || !element.HasAttribute("rel") || !element.HasAttribute("type"))
            {
                return results;
            }

            AttributeNode rel = element.GetAttribute("rel");

            if (rel.Value.Equals("logo", StringComparison.Ordinal))
            {
                AttributeNode type = element.GetAttribute("type");

                if (!type.Value.Equals("image/svg", StringComparison.OrdinalIgnoreCase))
                {
                    int index = element.Attributes.IndexOf(type);
                    results.AddAttributeError(element, "The type attribute value must be \"image/svg\" for rel=\"logo\" links.", HtmlValidationErrorLocation.AttributeValue, index);
                }
            }

            return results;
        }
    }
}
