﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using OpenQA.Selenium;
using OpenQA.Selenium.Internal;
using Selenium.Community.PageObjects.Proxies;

namespace Selenium.Community.PageObjects
{
    /// <summary>
    /// A PageObject Member decorator which applies proxies, this allows for lazy-access to the members
    /// </summary>
    public class ProxyPageObjectMemberDecorator : IPageObjectMemberDecorator
    {
        private readonly IElementActivator _elementActivator;
        private readonly PageObjectFactory _factory;

        /// <summary>
        /// Creates a new instance of <see cref="ProxyPageObjectMemberDecorator"/>
        /// </summary>
        /// <param name="elementActivator">The ElementActivator to use once proxied</param>
        /// <param name="factory">The PageObjectFactory to use for nested page objects</param>
        public ProxyPageObjectMemberDecorator(IElementActivator elementActivator, PageObjectFactory factory)
        {
            _elementActivator = elementActivator;
            _factory = factory;
        }

        /// <summary>
        /// Locates an element or list of elements for a Page Object member.
        /// </summary>
        /// <param name="typeToDecorate">The <see cref="Type"/> of the member to decorate</param>
        /// <param name="bys">The <see cref="By"> bys</see> provided to decorate the member with</param>
        /// <param name="elementLocator">The <see cref="IElementLocator"/> elementLocator to locate elements.</param>
        /// <returns>The Page Object's member value</returns>
        public object Decorate(Type typeToDecorate, IEnumerable<By> bys, IElementLocator elementLocator)
        {
            if (typeof(IWebElement).IsAssignableFrom(typeToDecorate))
            {
                return DecorateWebElement(elementLocator, bys);
            }

            if (typeof(IWrapsElement).IsAssignableFrom(typeToDecorate))
            {
                return DecorateWrappedWebElement(typeToDecorate, elementLocator, bys);
            }

            if (typeToDecorate.IsGenericType)
            {
                var genericTypeDefinition = typeToDecorate.GetGenericTypeDefinition();
                var genericTypeArgument = typeToDecorate.GenericTypeArguments.Single();

                if (typeof(IEnumerable<>).IsAssignableFrom(genericTypeDefinition))
                {
                    if (typeof(IWebElement).IsAssignableFrom(genericTypeArgument))
                    {
                        return WebElementEnumerableProxy.Create(elementLocator, bys);
                    }

                    if (typeof(IWrapsElement).IsAssignableFrom(genericTypeArgument))
                    {
                        var method = typeof(ProxyPageObjectMemberDecorator).GetMethod(nameof(DecorateEnumerableWrappedElement), new[] { typeof(IElementLocator), typeof(IEnumerable<By>) });
                        method = method.MakeGenericMethod(genericTypeArgument);
                        var element = method.Invoke(this, new object[] { elementLocator, bys });

                        return element;
                    }
                }
            }

            throw new DecorationException($"Unable to decorate {typeToDecorate.Name}, it is unsupported");
        }

        public IEnumerable<T> DecorateEnumerableWrappedElement<T>(IElementLocator elementLocator, IEnumerable<By> bys)
        {
            return WebElementEnumerableProxy.Create(elementLocator, bys)
                .Select(webElement => (T)CreateAndPopulateWrapsElement(typeof(T), webElement));
        }

        private object DecorateWebElement(IElementLocator elementLocator, IEnumerable<By> bys)
        {
            return WebElementProxy.Create(elementLocator, bys);
        }

        private object DecorateWrappedWebElement(Type typeToDecorate, IElementLocator elementLocator, IEnumerable<By> bys)
        {
            var element = WebElementProxy.Create(elementLocator, bys);

            return CreateAndPopulateWrapsElement(typeToDecorate, element);
        }

        private object CreateAndPopulateWrapsElement(Type typeToDecorate, IWebElement element)
        {
            var wrappedElement = _elementActivator.Create(typeToDecorate, element);
            var wrappedElementProperty = wrappedElement.GetType()
                .GetMember(nameof(IWrapsElement.WrappedElement))
                .Single() as PropertyInfo;

            if (wrappedElementProperty.CanWrite)
            {
                wrappedElementProperty.SetValue(wrappedElement, element);
            }
            _factory.InitElements(wrappedElement, new DefaultElementLocator(element));

            return wrappedElement;
        }
    }
}