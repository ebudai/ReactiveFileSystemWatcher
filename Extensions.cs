// Copyright (c) 2022 Eric Budai, All Rights Reserved
using System;
using System.Collections.Generic;
using System.Reactive.Linq;

namespace Budaisoft.FileSystem
{
    public static class Extensions
    {
        /// <summary>
        ///     This will publish an observable list of TSource when threshold has been exceeded, but only if there is at least one item
        /// </summary>
        /// <typeparam name="TSource">
        ///     the type of item to be buffered
        /// </typeparam>
        /// <param name="source">
        ///     the observable to buffer
        /// </param>
        /// <param name="threshold">
        ///     the time to wait before publishing
        /// </param>
        /// <returns>
        ///     the observable IList of TSource of buffered items
        /// </returns>
        public static IObservable<IList<TSource>> BufferWhenAvailable<TSource>(this IObservable<TSource> source, TimeSpan threshold)
        {
            return source.GroupByUntil(_ => true, _ => Observable.Timer(threshold)).SelectMany(i => i.ToList());
        }
    }
}
