﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace GeneralUpdate.Core.Files
{
    internal class FileEnumerator<T> : IEnumerator<T> where T : IComparable
    {
        private readonly bool asc;

        private FileNode<T> startNode;
        private FileNode<T> current;

        private int index;

        internal FileEnumerator(FileManager<T> tree, bool asc = true)
        {
            this.asc = asc;
            startNode = asc ? tree.BottomLeftNode : tree.BottomRightNode;
            current = startNode;
            index = asc ? -1 : current.KeyCount;
        }

        public bool MoveNext()
        {
            if (current == null)
            {
                return false;
            }

            if (asc)
            {
                if (index + 1 < current.KeyCount)
                {
                    index++;
                    return true;
                }
            }
            else
            {
                if (index - 1 >= 0)
                {
                    index--;
                    return true;
                }
            }

            current = asc ? current.Next : current.Prev;

            var canMove = current != null && current.KeyCount > 0;
            if (canMove)
            {
                index = asc ? 0 : current.KeyCount - 1;
            }

            return canMove;
        }

        public void Reset()
        {
            current = startNode;
            index = asc ? -1 : current.KeyCount;
        }

        object IEnumerator.Current => Current;

        public T Current
        {
            get
            {
                return current.Keys[index];
            }
        }

        public void Dispose()
        {
            current = null;
            startNode = null;
        }
    }
}
