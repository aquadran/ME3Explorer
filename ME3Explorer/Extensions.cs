﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace ME3Explorer
{
    public static class WPFExtensions
    {
        /// <summary>
        /// Binds a property
        /// </summary>
        /// <param name="bound">object to create binding on</param>
        /// <param name="boundProp">property to create binding on</param>
        /// <param name="source">object being bound to</param>
        /// <param name="sourceProp">property being bound to</param>
        /// <param name="converter">optional value converter</param>
        /// <param name="parameter">optional value converter parameter</param>
        public static void bind(this FrameworkElement bound, DependencyProperty boundProp, FrameworkElement source, string sourceProp,
            IValueConverter converter = null, object parameter = null)
        {
            Binding b = new Binding { Source = source, Path = new PropertyPath(sourceProp) };
            if (converter != null)
            {
                b.Converter = converter;
                b.ConverterParameter = parameter;
            }
            bound.SetBinding(boundProp, b);
        }
    }

    public static class ListExtensions
    {
        public static int FindOrAdd<T>(this List<T> list, T element)
        {
            int idx = list.IndexOf(element);
            if (idx == -1)
            {
                list.Add(element);
                idx = list.Count - 1;
            }
            return idx;
        }
        
        public static IEnumerable<T> Concat<T>(this IEnumerable<T> first, T second)
        {
            foreach (T element in first)
            {
                yield return element;
            }
            yield return second;
        }

        /// <summary>
        /// Searches for the specified object and returns the index of its first occurence, or -1 if it is not found
        /// </summary>
        /// <param name="array">The one-dimensional array to search</param>
        /// <param name="value">The object to locate in <paramref name="array" /></param>
        /// <typeparam name="T">The type of the elements of the array.</typeparam>
        /// <exception cref="T:System.ArgumentNullException">
        /// <paramref name="array" /> is null.</exception>
        public static int IndexOf<T>(this T[] array, T value)
        {
            return Array.IndexOf(array, value);
        }
    }
}