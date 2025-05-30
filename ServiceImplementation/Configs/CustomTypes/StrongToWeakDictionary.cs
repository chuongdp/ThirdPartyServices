﻿namespace ServiceImplementation.Configs.CustomTypes
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEngine;

    /// <summary>
    /// Weak value reference dictionary.
    /// </summary>
    /// <remarks>
    /// Credit: https://blogs.msdn.microsoft.com/jaredpar/2009/03/03/building-a-weakreference-hashtable/
    /// </remarks>
    public sealed class StrongToWeakDictionary<TKey, TValue> where TValue : class
    {
        private static bool VerboseDebug = false;

        // Underlying dictionary that actually holds the data.
        private readonly Dictionary<TKey, WeakReference<TValue>> mMap;

        // Serves as a simple "GC Monitor" that indicates whether cleanup is needed.
        // If _gcSentinal.IsAlive is false, GC has occurred and we should perform cleanup
        private WeakReference mGcWatch = AllocWeakRef();

        /// <summary>
        /// Gets a list of key value entries stored in this dictionary.
        /// Note that the values of these are alive at the returning moment,
        /// but the garbage collector can kick in immediately after return
        /// and reclaim some of them.
        /// </summary>
        /// <value>The pairs.</value>
        public List<KeyValuePair<TKey, TValue>> Pairs
        {
            get
            {
                return this.mMap
                    .Select(p => new KeyValuePair<TKey, WeakReference<TValue>>(p.Key, p.Value))
                    .Where(t => t.Value.IsAlive)
                    .Select(t => new KeyValuePair<TKey, TValue>(t.Key, t.Value.Target))
                    .ToList();
            }
        }

        /// <summary>
        /// Gets the values stored in this dictionary. Note that these objects 
        /// are alive at the returning moment, but the garbage collector can 
        /// kick in immediately after return and reclaim some of them.
        /// </summary>
        /// <value>The values.</value>
        public List<TValue> Values { get { return this.Pairs.Select(x => x.Value).ToList(); } }

        public StrongToWeakDictionary()
            : this(EqualityComparer<TKey>.Default)
        {
        }

        public StrongToWeakDictionary(IEqualityComparer<TKey> comparer)
        {
            this.mMap = new(comparer);
        }

        public void Add(TKey key, TValue value)
        {
            this.mMap.Add(key, WeakReference<TValue>.Create(value));
            this.CullIfNeeded();
        }

        public void Put(TKey key, TValue value)
        {
            this.mMap[key] = WeakReference<TValue>.Create(value);
            this.CullIfNeeded();
        }

        public bool Remove(TKey key)
        {
            this.CullIfNeeded();
            return this.mMap.Remove(key);
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            WeakReference<TValue> weakValue;
            if (this.mMap.TryGetValue(key, out weakValue))
            {
                // A strong reference is being assigned to Target,
                // safely excluding it from being GC'ed. IsAlive == true
                // is fully trustable in this case!
                value = weakValue.Target;
                return weakValue.IsAlive;
            }
            else
            {
                value = default;
                return false;
            }
        }

        /// <summary>
        /// Performs a full scan & cull of collected weak references if GC has occurred.
        /// </summary>
        private void CullIfNeeded()
        {
            if (!this.mGcWatch.IsAlive)
            {
                if (VerboseDebug) Debug.Log(typeof(TValue).ToString() + " StrongToWeakDict: GC has occurred. Start culling dict...");

                this.CullDeadEntries();
                this.mGcWatch = AllocWeakRef(); // spawn new watch
            }
        }

        /// <summary>
        /// Performs a full scan over the dictionary and removes
        /// left-over weak references for entries in the dictionary
        /// whose value has been reclaimed by the garbage collector.
        /// </summary>
        public void CullDeadEntries()
        {
            if (VerboseDebug) Debug.Log(typeof(TValue).ToString() + " StrongToWeakDict: count BEFORE culling: " + this.mMap.Count);

            List<TKey> toRemove = null;
            foreach (var pair in this.mMap)
            {
                var key       = pair.Key;
                var weakValue = pair.Value;

                if (!weakValue.IsAlive)
                {
                    if (toRemove == null) toRemove = new();
                    toRemove.Add(key);
                }
            }

            if (toRemove != null)
                foreach (var key in toRemove)
                    this.mMap.Remove(key);

            if (VerboseDebug) Debug.Log(typeof(TValue).ToString() + " StrongToWeakDict: count AFTER culling: " + this.mMap.Count);
        }

        /// <summary>
        /// Returns a weak reference to a sacrificial object that is
        /// abandoned immediately after created. This weak reference
        /// therefore can be used to check if a GC has occurred recently
        /// (if that happens, the reference will be dead).
        /// </summary>
        /// <returns>The gc watch.</returns>
        private static WeakReference AllocWeakRef()
        {
            return new(new());
        }

        // Adds strong typing to WeakReference.Target using generics. Also,
        // the Create factory method is used in place of a constructor
        // to handle the case where target is null, but we want the
        // reference to still appear to be alive.
        internal class WeakReference<T> : WeakReference where T : class
        {
            public static WeakReference<T> Create(T target)
            {
                if (target == null) return WeakNullReference<T>.Singleton;

                return new(target);
            }

            protected WeakReference(T target)
                : base(target, false)
            {
            }

            public new T Target => (T)base.Target;
        }

        // Provides a weak reference to a null target object, which, unlike
        // other weak references, is always considered to be alive. This
        // facilitates handling null dictionary values, which are perfectly
        // legal.
        internal class WeakNullReference<T> : WeakReference<T> where T : class
        {
            public static readonly WeakNullReference<T> Singleton = new();

            private WeakNullReference()
                : base(null)
            {
            }

            public override bool IsAlive => true;
        }
    }
}