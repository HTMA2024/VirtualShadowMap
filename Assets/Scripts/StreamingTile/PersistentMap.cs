using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AdaptiveRendering
{
    public class PersistentMap
    {
    }

    [Serializable]
    public class PersistentMap<TKey, TValue> :
        PersistentMap,
        ISerializationCallbackReceiver,
        IDictionary<TKey, TValue>
    {
        [SerializeField] private List<SerializableKeyValuePair> list = new List<SerializableKeyValuePair>();

        [Serializable]
        private struct SerializableKeyValuePair
        {
            public TKey Key;
            public TValue Value;


            public SerializableKeyValuePair(TKey key, TValue value)
            {
                Key = key;
                Value = value;
            }
        }



        #region key index

        private Dictionary<TKey, int> KeyPositions
        {
            get { return _keyPositions.Value; }
        }

        private Lazy<Dictionary<TKey, int>> _keyPositions;


        public PersistentMap()
        {
            _keyPositions = new Lazy<Dictionary<TKey, int>>(MakeKeyPositions);
        }

        #endregion



        private Dictionary<TKey, int> MakeKeyPositions()
        {
            Dictionary<TKey, int> dictionary = new Dictionary<TKey, int>(list.Count);
            for (var i = 0; i < list.Count; i++)
            {
                dictionary[list[i].Key] = i;
            }

            return dictionary;
        }


        public void OnBeforeSerialize()
        {
        }


        public void OnAfterDeserialize()
        {
            _keyPositions = new Lazy<Dictionary<TKey, int>>(MakeKeyPositions);
        }



        #region IDictionary<TKey, TValue>

        /// <summary>
        ///  array[index]   dic[key]
        /// </summary>
        /// <param name="key"></param>
        public TValue this[TKey key]
        {
            get => list[KeyPositions[key]].Value;
            set
            {
                SerializableKeyValuePair pair = new SerializableKeyValuePair(key, value);
                if (KeyPositions.ContainsKey(key))
                {
                    list[KeyPositions[key]] = pair;
                }
                else
                {
                    KeyPositions[key] = list.Count;
                    list.Add(pair);
                }
            }
        }

        public ICollection<TKey> Keys
        {
            get { return list.Select(tuple => tuple.Key).ToArray(); }
        }

        public ICollection<TValue> Values
        {
            get { return list.Select(tuple => tuple.Value).ToArray(); }
        }


        public void Add(TKey key, TValue value)
        {
            if (KeyPositions.ContainsKey(key))
                throw new ArgumentException("An element with the same key already exists in the dictionary.");
            else
            {
                KeyPositions[key] = list.Count;
                list.Add(new SerializableKeyValuePair(key, value));
            }
        }


        public bool ContainsKey(TKey key)
        {
            return KeyPositions.ContainsKey(key);
        }


        public bool Remove(TKey key)
        {
            if (KeyPositions.TryGetValue(key, out var index))
            {
                KeyPositions.Remove(key);

                list.RemoveAt(index);
                for (var i = index; i < list.Count; i++)
                    KeyPositions[list[i].Key] = i;

                return true;
            }
            else
                return false;
        }


        public bool TryGetValue(TKey key, out TValue value)
        {
            if (KeyPositions.TryGetValue(key, out var index))
            {
                value = list[index].Value;
                return true;
            }
            else
            {
                value = default;
                return false;
            }
        }

        #endregion



        #region ICollection <KeyValuePair<TKey, TValue>>

        public int Count
        {
            get { return list.Count; }
        }

        public bool IsReadOnly
        {
            get { return false; }
        }


        public void Add(KeyValuePair<TKey, TValue> kvp)
        {
            Add(kvp.Key, kvp.Value);
        }


        public void Clear()
        {
            list.Clear();
        }


        public bool Contains(KeyValuePair<TKey, TValue> kvp)
        {
            return KeyPositions.ContainsKey(kvp.Key);
        }


        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            var numKeys = list.Count;
            if (array.Length - arrayIndex < numKeys)
                throw new ArgumentException("arrayIndex");
            for (var i = 0; i < numKeys; i++, arrayIndex++)
            {
                var entry = list[i];
                array[arrayIndex] = new KeyValuePair<TKey, TValue>(entry.Key, entry.Value);
            }
        }


        public bool Remove(KeyValuePair<TKey, TValue> kvp)
        {
            return Remove(kvp.Key);
        }

        #endregion



        #region IEnumerable <KeyValuePair<TKey, TValue>>

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            return list.Select(ToKeyValuePair).GetEnumerator();


        }

        static KeyValuePair<TKey, TValue> ToKeyValuePair(SerializableKeyValuePair skvp)
        {
            return new KeyValuePair<TKey, TValue>(skvp.Key, skvp.Value);
        }


        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        #endregion
    }


#if UNITY_EDITOR

    [UnityEditor.CustomPropertyDrawer(typeof(PersistentMap), true)]
    public class PersistentMapDrawer : UnityEditor.PropertyDrawer
    {
        private UnityEditor.SerializedProperty listProperty;


        private UnityEditor.SerializedProperty getListProperty(UnityEditor.SerializedProperty property)
        {

            if (listProperty == null)
            {
                return listProperty = property.FindPropertyRelative("list");
            }

            return listProperty;
        }


        public override void OnGUI(Rect position, UnityEditor.SerializedProperty property, GUIContent label)
        {
            UnityEditor.EditorGUI.PropertyField(position, getListProperty(property), label, true);
        }


        public override float GetPropertyHeight(UnityEditor.SerializedProperty property, GUIContent label)
        {
            return UnityEditor.EditorGUI.GetPropertyHeight(getListProperty(property), true);
        }
    }

#endif
}