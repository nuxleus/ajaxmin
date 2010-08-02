// simplehashtable.cs
//
// Copyright 2010 Microsoft Corporation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections;

namespace Microsoft.Ajax.Utilities
{
  internal sealed class SimpleHashtable
  {
    private HashtableEntry[] m_table;
    private uint m_threshold;
    private int m_count;
    public int Count { get { return m_count; } }

    public SimpleHashtable(uint threshold)
    {
      if (threshold < 8)
        threshold = 8;
      m_table = new HashtableEntry[(int)(threshold * 2 - 1)];
      m_threshold = threshold;
    }

    public IDictionaryEnumerator GetEnumerator()
    {
      return new SimpleHashtableEnumerator(m_table);
    }

    private HashtableEntry GetHashtableEntry(Object key, uint hashCode)
    {
      int index = (int)(hashCode % (uint)m_table.Length);
      HashtableEntry e = m_table[index];
      if (e == null) return null;
      if (e.Key == key) return e;
      HashtableEntry curr = e.Next;
      while (curr != null)
      {
        if (curr.Key == key)
          return curr;
        curr = curr.Next;
      }
      if (e.HashCode == hashCode && e.Key.Equals(key))
      {
        e.Key = key; return e;
      }
      curr = e.Next;
      while (curr != null)
      {
        if (curr.HashCode == hashCode && curr.Key.Equals(key))
        {
          curr.Key = key; return curr;
        }
        curr = curr.Next;
      }
      return null;
    }

    private void Rehash()
    {
      HashtableEntry[] oldTable = m_table;
      uint threshold = m_threshold = (uint)(oldTable.Length + 1);
      uint newCapacity = threshold * 2 - 1;
      HashtableEntry[] newTable = m_table = new HashtableEntry[newCapacity];
      for (uint i = threshold - 1; i-- > 0; )
      {
        for (HashtableEntry old = oldTable[(int)i]; old != null; )
        {
          HashtableEntry e = old; old = old.Next;
          int index = (int)(e.HashCode % newCapacity);
          e.Next = newTable[index];
          newTable[index] = e;
        }
      }
    }

    public Object this[Object key]
    {
      get
      {
        HashtableEntry e = GetHashtableEntry(key, (uint)key.GetHashCode());
        if (e == null) return null;
        return e.Value;
      }
      set
      {
        uint hashCode = (uint)key.GetHashCode();
        HashtableEntry e = GetHashtableEntry(key, hashCode);
        if (e != null)
        {
          e.Value = value; return;
        }
        if (++m_count >= m_threshold) Rehash();
        int index = (int)(hashCode % (uint)m_table.Length);
        m_table[index] = new HashtableEntry(key, value, hashCode, m_table[index]);
      }
    }


    private sealed class SimpleHashtableEnumerator : IDictionaryEnumerator
    {
      private HashtableEntry[] m_table;
      private int m_count;
      private int m_index;
      private HashtableEntry m_currentEntry;

      public SimpleHashtableEnumerator(HashtableEntry[] table)
      {
        m_table = table;
        m_count = table.Length;
        m_index = -1;
      }

      public Object Current
      { //Used by expando classes to enumerate all the keys in the hashtable
        get
        {
          return Key;
        }
      }

      public DictionaryEntry Entry
      {
        get
        {
          return new DictionaryEntry(Key, Value);
        }
      }

      public Object Key
      {
        get
        {
          return m_currentEntry.Key;
        }
      }

      public bool MoveNext()
      {
        HashtableEntry[] table = m_table;
        if (m_currentEntry != null)
        {
          m_currentEntry = m_currentEntry.Next;
          if (m_currentEntry != null)
            return true;
        }
        for (int i = ++m_index, n = m_count; i < n; i++)
          if (table[i] != null)
          {
            m_index = i;
            m_currentEntry = table[i];
            return true;
          }
        return false;
      }

      public void Reset()
      {
        m_index = -1;
        m_currentEntry = null;
      }

      public Object Value
      {
        get
        {
          return m_currentEntry.Value;
        }
      }

    }

    private sealed class HashtableEntry
    {
      public Object Key;
      public Object Value;
      public uint HashCode;
      public HashtableEntry Next;

      public HashtableEntry(Object key, Object value, uint hashCode, HashtableEntry next)
      {
        Key = key;
        Value = value;
        HashCode = hashCode;
        Next = next;
      }
    }
  }

}
    
