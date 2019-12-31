using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Web.Mvc;
using CustomAttributeForPopOver.Resources;

namespace CustomAttributeForPopOver.Components
{
    /// <summary>
    /// Localizable string is a class which is being used to contain the resource and name of the resource,
    /// or if the resource is not being used, then just the name of the item
    /// </summary>
    internal class LocalizableString
    {
        private readonly string _propertyName;
        private string _propertyValue;
        private Type _resourceType;
        private Func<string> _cachedResult;

        public LocalizableString(string propertyName)
        {
            _propertyName = propertyName;
        }

        public string Value
        {
            get => _propertyValue;
            set
            {
                if (_propertyValue == value)
                    return;
                ClearCache();
                _propertyValue = value;
            }
        }

        public Type ResourceType
        {
            get => _resourceType;
            set
            {
                if (!(_resourceType != value))
                    return;
                ClearCache();
                _resourceType = value;
            }
        }

        private void ClearCache()
        {
            _cachedResult = null;
        }
        /// <summary>
        /// In case the resource type is set to this object, the name which is set to object will be used as the name of the resource
        /// If the resource type is not set, the name will be used as a constant
        /// </summary>
        /// <returns>The result string based on if the resource was set or not</returns>
        public string GetLocalizableValue()
        {
            if (_cachedResult == null)
            {
                if (_propertyValue == null || _resourceType == null)
                {
                    _cachedResult = () => _propertyValue;
                }
                else
                {
                    PropertyInfo property = _resourceType.GetProperty(_propertyValue);
                    bool flag = false;
                    if (!_resourceType.IsVisible || property == null || property.PropertyType != typeof(string))
                    {
                        flag = true;
                    }
                    else
                    {
                        var getMethod = property.GetGetMethod();
                        if (getMethod == null || !getMethod.IsPublic || !getMethod.IsStatic)
                            flag = true;
                    }
                    if (flag)
                    {
                        string exceptionMessage = string.Format(CultureInfo.CurrentCulture, "{0} {1} {2}", new object[]
                        {
                          _propertyName,
                          _resourceType.FullName,
                          _propertyValue
                        });
                        _cachedResult = () => throw new InvalidOperationException(exceptionMessage);
                    }
                    else
                        _cachedResult = () => (string)property.GetValue(null, null);
                }
            }
            return _cachedResult();
        }
    }
    /// <summary>
    /// I have three parameters to this Attribute, since the PopOver contains two fields and third is the type of the resource
    /// Properties:
    /// 1. Title - The title of the PopOver
    /// 2. Content - The main text part of the PopOver
    /// </summary>
    public class DataContentAnnotation : Attribute
    {
        private readonly LocalizableString _title = new LocalizableString(nameof(Title));
        private readonly LocalizableString _content = new LocalizableString(nameof(Content));
        private Type _resourceType;

        public Type ResourceType
        {
            get => _resourceType;
            set
            {
                if (!(_resourceType != value))
                    return;

                _resourceType = value;
                _title.ResourceType = value;
                _content.ResourceType = value;
            }
        }

        public string Title
        {
            get => _title.GetLocalizableValue();
            set
            {
                if (_title.Value == value)
                    return;
                _title.Value = value;
            }
        }

        public string Content
        {
            get => _content.GetLocalizableValue();
            set
            {
                if (_content.Value == value)
                    return;
                _content.Value = value;
            }
        }
    }

    public static class HtmlExtensionsLabel
    {
        /// <summary>
        /// This is an extension method to @Html class to extend the LabelFor method to use Pop-over attributes on tag
        /// </summary>
        /// <typeparam name="TModel"></typeparam>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="html"></param>
        /// <param name="expression"></param>
        /// <param name="htmlAttributes"></param>
        /// <returns></returns>
        public static MvcHtmlString LabelForExtended<TModel, TValue>(
            this HtmlHelper<TModel> html,
            Expression<Func<TModel, TValue>> expression,
            object htmlAttributes)
        {
            var attributes = HtmlHelper.AnonymousObjectToHtmlAttributes(htmlAttributes);

            // Get the name of the Model's property to make sure that we have something that has a parameter
            var name = expression.Body.ToString().Split('.').LastOrDefault();
            if (string.IsNullOrEmpty(name))
                return LabelHelper(html,
                    ModelMetadata.FromLambdaExpression(expression, html.ViewData),
                    ExpressionHelper.GetExpressionText(expression),
                    null,
                    attributes);

            // Get property itself
            var property = expression.Type.GenericTypeArguments[0].GetProperty(name);
            if (property == null)
                return LabelHelper(html,
                    ModelMetadata.FromLambdaExpression(expression, html.ViewData),
                    ExpressionHelper.GetExpressionText(expression),
                    null,
                    attributes);

            // Get our custom Attribute - DataContentAnnotation
            var attr = property.CustomAttributes
                .FirstOrDefault(e => e.AttributeType == typeof(DataContentAnnotation));

            string title = "", content = "";

            if(attr == null)
            {
                if(property.DeclaringType != null)
                {
                    var metadataType = property.DeclaringType.GetCustomAttributes(typeof(MetadataTypeAttribute), true)
                        .OfType<MetadataTypeAttribute>().FirstOrDefault();
               
                    if(metadataType != null)
                    {
                        property = metadataType.MetadataClassType.GetProperty(name);

                        if (property?.GetCustomAttributes(typeof(DataContentAnnotation), false)
                            .FirstOrDefault() is DataContentAnnotation attrFromMetadata)
                        {
                            title = attrFromMetadata.Title;
                            content = attrFromMetadata.Content;
                        }
                    }
                }
            }
            else
            {
                var explanation = new DataContentAnnotation
                {
                    Title = attr.NamedArguments?.Where(e => e.MemberName == "Title").Select(e => e.TypedValue.Value.ToString()).FirstOrDefault(),
                    Content = attr.NamedArguments?.Where(e => e.MemberName == "Content").Select(e => e.TypedValue.Value.ToString()).FirstOrDefault(),
                    ResourceType = typeof(Labels)
                };

                title = explanation.Title;
                content = explanation.Content;
            }

            if (string.IsNullOrEmpty(title) && string.IsNullOrEmpty(content))
                return LabelHelper(html,
                    ModelMetadata.FromLambdaExpression(expression, html.ViewData),
                    ExpressionHelper.GetExpressionText(expression),
                    null,
                    attributes);

            attributes.Add("data-toggle", "popover");
            attributes.Add("data-original-title", title);
            attributes.Add("data-content", content);

            return LabelHelper(html,
                ModelMetadata.FromLambdaExpression(expression, html.ViewData),
                ExpressionHelper.GetExpressionText(expression),
                null,
                attributes);
        }

        // Label rendering method
        internal static MvcHtmlString LabelHelper(
            HtmlHelper html,
            ModelMetadata metadata,
            string htmlFieldName,
            string labelText = null,
            IDictionary<string, object> htmlAttributes = null)
        {
            var str = labelText;
            if (str == null)
            {
                var displayName = metadata.DisplayName;
                if (displayName == null)
                {
                    var propertyName = metadata.PropertyName;
                    str = propertyName ?? htmlFieldName.Split('.').Last();
                }
                else
                    str = displayName;
            }
            var innerText = str;
            if (string.IsNullOrEmpty(innerText))
                return MvcHtmlString.Empty;
            var tagBuilder = new TagBuilder("label");
            tagBuilder.Attributes.Add("for", TagBuilder.CreateSanitizedId(html.ViewContext.ViewData.TemplateInfo.GetFullHtmlFieldName(htmlFieldName)));
            tagBuilder.SetInnerText(innerText);
            tagBuilder.MergeAttributes(htmlAttributes, true);
            return MvcHtmlString.Create(tagBuilder.ToString(TagRenderMode.Normal));
        }
    }
}