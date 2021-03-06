﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using OpenQA.Selenium;

namespace Selenium.Community.PageObjects
{
    /// <summary>
    /// Provides the ability to produce Page Objects modeling a page
    /// </summary>
    public class PageObjectFactory
    {
        private readonly IElementLocator _elementLocator;
        private readonly IPageObjectMemberDecorator _pageObjectMemberDecorator;

        private const BindingFlags PublicBindingOptions = BindingFlags.Instance | BindingFlags.Public;
        private const BindingFlags NonPublicBindingOptions = BindingFlags.Instance | BindingFlags.NonPublic;

        /// <summary>
        /// Initializes a new instance of the <see cref="PageObjectFactory"/> class.
        /// </summary>
        /// <param name="webDriver">The WebDriver used to communicate with</param>
        public PageObjectFactory(IWebDriver webDriver)
        {
            _elementLocator = new DefaultElementLocator(webDriver);
            _pageObjectMemberDecorator = new ProxyPageObjectMemberDecorator(new DefaultElementActivator(webDriver), this);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PageObjectFactory"/> class.
        /// </summary>
        /// <param name="elementLocator">The locator used to locate members</param>
        /// <param name="pageObjectMemberDecorator">The MemberDecorator to use once members are found</param>
        /// <exception cref="ArgumentNullException">Thrown when either ElementLocator or PageObjectMemberDecorator are null</exception>
        public PageObjectFactory(IElementLocator elementLocator, IPageObjectMemberDecorator pageObjectMemberDecorator)
        {
            _elementLocator = elementLocator ?? throw new ArgumentNullException(nameof(elementLocator));
            _pageObjectMemberDecorator = pageObjectMemberDecorator ?? throw new ArgumentNullException(nameof(pageObjectMemberDecorator));
        }

        /// <summary>
        /// Populates the members decorated with the <see cref="FindsByAttribute"/>
        /// of the PageObject
        /// </summary>
        /// <param name="page">The pageObject</param>
        /// <exception cref="System.ArgumentNullException">Thrown when passed page is</exception>
        public void InitElements(object page)
        {
            if (page == null)
            {
                throw new ArgumentNullException(nameof(page));
            }

            InitElements(page, _elementLocator);
        }

        internal void InitElements(object page, IElementLocator locator)
        {
            foreach (var member in MembersToDecorate(page))
            {
                var bys = member.GetCustomAttributes()
                    .Select(x => (x as ByAttribute)?.ByFinder())
                    .Where(x => x != null)
                    .Distinct()
                    .ToArray();

                if (bys.Any())
                {
                    //Decorates the member
                    if (CanWriteToMember(member, out var typeToDecorate))
                    {
                        var decoratedValue = _pageObjectMemberDecorator.Decorate(typeToDecorate, bys, locator);
                        if (decoratedValue != null)
                        {
                            var field = member as FieldInfo;
                            var property = member as PropertyInfo;
                            if (field != null)
                            {
                                field.SetValue(page, decoratedValue);
                            }
                            else if (property != null)
                            {
                                property.SetValue(page, decoratedValue, null);
                            }
                        }
                    }
                    else
                    {
                        throw new DecorationException($"Unable to decorate {member.DeclaringType?.Name}.{member.Name}, it cannot be written to");
                    }
                }
            }
        }

        private static List<MemberInfo> MembersToDecorate(object page)
        {
            var type = page.GetType();
            var members = new List<MemberInfo>();
            members.AddRange(type.GetFields(PublicBindingOptions));
            members.AddRange(type.GetProperties(PublicBindingOptions));
            while (type != null)
            {
                members.AddRange(type.GetFields(NonPublicBindingOptions));
                members.AddRange(type.GetProperties(NonPublicBindingOptions));
                type = type.BaseType;
            }

            return members;
        }

        private static bool CanWriteToMember(MemberInfo member, out Type type)
        {
            type = null;
            var result = false;

            if (member is FieldInfo field)
            {
                type = field.FieldType;
                result = true;
            }

            if (member is PropertyInfo property)
            {
                type = property.PropertyType;
                result = property.CanWrite;
            }

            return result;
        }
    }
}
