using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NsLib.Utils;

namespace NsLib.PVS {

    // 外部接口，继承才能有大小
    public interface IHasRect {
        Rect Rectangle { get; }
    }

    internal static class RectHelper {

        public static float kEpsilon = 0.00002f;

        public static bool IsContains(this Rect r1, Rect r2) {
            bool x1 = (r1.x < r2.x) || (Mathf.Abs(r1.x - r2.x) <= kEpsilon);
            if (!x1)
                return false;
            float xx1 = r1.x + r1.width;
            float xx2 = r2.x + r2.width;
            bool x2 = xx1 > xx2 || (Mathf.Abs(xx1 - xx2) <= kEpsilon);
            if (!x2)
                return false;
            //float yyy = r1.y - r2.y;
            bool y1 = r1.y < r2.y || (Mathf.Abs(r1.y - r2.y) <= kEpsilon);
            if (!y1)
                return false;
            float yy1 = r1.y + r1.height;
            float yy2 = r2.y + r2.height;
            bool y2 = yy1 > yy2 || (Mathf.Abs(yy1 - yy2) <= kEpsilon);
            if (!y2)
                return false;
            return true;
        }

        // 子结构是否包含
        public static bool IsChildsContains(this Rect r1, Rect r2) {

            float halfWidth = (r1.width / 2f);
            float halfHeight = (r1.height / 2f);

            Vector2 min = r1.min;
            Vector2 halfSz = new Vector2(halfWidth, halfHeight);
            Rect child1 = new Rect(r1.position, halfSz);
            if (child1.IsContains(r2))
                return true;

            Rect child2 = new Rect(new Vector2(min.x, min.y + halfHeight), halfSz);
            if (child2.IsContains(r2))
                return true;

            Rect child3 = new Rect(new Vector2(min.x + halfWidth, min.y), halfSz);
            if (child3.IsContains(r2))
                return true;

            Rect child4 = new Rect(new Vector2(min.x + halfWidth, min.y + halfHeight), halfSz);
            if (child4.IsContains(r2))
                return true;
            return false;
        }

        public static bool IntersectsWith(this Rect r1, Rect r2) {
			var maxax = r1.x + r1.width;
			var maxay = r1.y + r1.height;
			var maxbx = r2.x + r2.width;
			var maxby = r2.y + r2.height;
			bool ret = !(maxax < r2.x || maxbx < r1.x || maxay < r2.y || maxby < r1.y);
			return ret;
        }
    }


    // PoolNode是线程安全的
    public class QuadTreeNodeArray<T> : PoolNode<QuadTreeNodeArray<T>> where T : IHasRect {
        private QuadTreeNode<T>[] m_Array = null;

        public QuadTreeNodeArray() {
            m_Array = new QuadTreeNode<T>[4];
        }

        public int Count {
            get {
                if (m_Array == null)
                    return 0;
                return m_Array.Length;
            }
        }

        public QuadTreeNode<T> this[int idx] {
            get {
                if (m_Array == null)
                    return null;
                if (idx >= 0 && idx < m_Array.Length) {
                    return m_Array[idx];
                }
                return null;
            }

            set {
                if (m_Array == null)
                    return;
                if (idx >= 0 && idx < m_Array.Length) {
                    var oldNode = m_Array[idx];
                    if (oldNode != null) {
                        oldNode.Dispose();
                    }
                    m_Array[idx] = value;
                }
            }
        }

        public QuadTreeNode<T>[] Array {
            get {
                return m_Array;
            }
        }

        protected override void OnFree() {
            base.OnFree();
            if (m_Array != null) {
                for (int i = 0; i < m_Array.Length; ++i) {
                    var node = m_Array[i];
                    if (node != null) {
                        node.Dispose();
                    }
                }
                System.Array.Clear(m_Array, 0, m_Array.Length);
            }
        }
    }

    public class QuadTreeNode<T>: PoolNode<QuadTreeNode<T>> where T : IHasRect {

        public delegate void QTAction(QuadTreeNode<T> node);

        private Rect m_bounds;
        private QuadTreeNodeArray<T> m_Childs = null;
        private List<T> m_Contents = null;

        public QuadTreeNodeArray<T> Childs {
            get {
                return m_Childs;
            }
        }

        public void RemoveAllChilds() {
            if (m_Childs != null) {
                m_Childs.Dispose();
                m_Childs = null;
            }
        }

        public void RemoveAllContents() {
            if (m_Contents != null)
                m_Contents.Clear();
        }

        public T GetItem(int idx) {
            if (m_Contents == null || idx < 0 || idx >= m_Contents.Count)
                return default(T);
            return m_Contents[idx];
        }

        private void InitCOntents() {
            if (m_Contents == null)
                m_Contents = new List<T>();
        }

        private void InitChilds() {
            if (m_Childs == null) {
                m_Childs = AbstractPool<QuadTreeNodeArray<T>>.GetNode() as QuadTreeNodeArray<T>;
            }

        }

        protected override void OnFree() {
            base.OnFree();
			RemoveAllContents ();
            if (m_Childs != null) {
                m_Childs.Dispose();
                m_Childs = null;
            }
        }

        public QuadTreeNode() { }

        internal static QuadTreeNode<T> CreateQuadTreeNode(Rect bound) {
            QuadTreeNode<T> ret = AbstractPool<QuadTreeNode<T>>.GetNode() as QuadTreeNode<T>;
            ret.Init(bound);
            return ret;
        }

        public void Init(Rect bounds) {
            m_bounds = bounds;
        }

        public bool IsEmpty {
            get {
                var sz = m_bounds.size;
                bool ret = (Mathf.Abs(sz.x) <= Vector3.kEpsilon) && (Mathf.Abs(sz.y) <= Vector3.kEpsilon);
                return ret;
            }
        }

        public Rect Bounds {
            get {
                return m_bounds;
            }
        }

        /// <summary>
        /// 获得Item数量，不要频繁调用，有性能问题
        /// </summary>
        public int Count {
            get {
                int ret = 0;
                if (m_Childs != null) {
                    for (int i = 0; i < m_Childs.Count; ++i) {
                        var node = m_Childs[i];
                        if (node != null) {
                            ret += node.Count;
                        }
                    }
                }

                if (m_Contents != null) {
                    ret += m_Contents.Count;
                }

                return ret;

            }
        }

        public List<T> Contents {
            get {
                return m_Contents;
            }
        }

        public bool Insert(T item, QuadTree<T> tree) {
            if (item == null || tree == null)
                return false;
            if (!m_bounds.IsContains(item.Rectangle)) {
                return false;
            }

            if (m_bounds.IsChildsContains(item.Rectangle)) {
                if (m_Childs == null)
                    CreateSubNodes(tree.LimitWidthMulHeightSize);

                if (m_Childs == null)
                    return false;
                for (int i = 0; i < m_Childs.Count; ++i) {
                    var node = m_Childs[i];
                    if (node.Bounds.IsContains(item.Rectangle)) {
                        return node.Insert(item, tree);
                    }
                }
            }

            InitCOntents();
            m_Contents.Add(item);
            //Debug.LogFormat("QuadTree Insert: Center({0}) Size({1})", m_bounds.center.ToString(), m_bounds.size.ToString());
            return true;
        }

        private bool IsChildsMustCombine() {
            if (m_Childs == null || m_Childs.Count <= 0)
                return true;
            for (int i = 0; i < m_Childs.Count; ++i) {
                var node = m_Childs[i];
                if (node.Count > 0)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// onCombineAction 性能不太好，不要频繁，不带onCombineAction不会合并Child
        /// </summary>
        /// <param name="item">格子内物品</param>
        /// <param name="onCombineAction">合并格子的回调</param>
        /// <returns></returns>
        public bool RemoveItem(T item, QTAction onCombineAction = null) {
            if (item == null)
                return false;
            if (!m_bounds.IsContains(item.Rectangle))
                return false;

            if (m_Childs != null && m_bounds.IsChildsContains(item.Rectangle)) {
                for (int i = 0; i < m_Childs.Count; ++i) {
                    var node = m_Childs[i];
                    if (node.Bounds.IsContains(item.Rectangle)) {
                        bool ret = node.RemoveItem(item, onCombineAction);
                        if (ret && (onCombineAction != null)) {
                            if (IsChildsMustCombine()) {
                                onCombineAction(node);
                            }
                        }
                        return ret;
                    }
                }
            }

            if (m_Contents != null) {
                for (int i = 0; i < m_Contents.Count; ++i) {
                    var content = m_Contents[i];
                    if (content.Equals(item)) {
                        m_Contents.RemoveAt(i);
                        return true;
                    }
                }
            }

            return false;

        }

        

        public void ForEach(QTAction action, bool isFirstSelf) {
            if (action == null)
                return;
            if (isFirstSelf)
                action(this);

            if (m_Childs != null) {
                for (int i = m_Childs.Count - 1; i >= 0; --i) {
                    var node = m_Childs[i];
                    node.ForEach(action, isFirstSelf);
                }
            }

            if (!isFirstSelf)
                action(this);
        }

        internal void SubTreeContents(List<T> results) {
            if (results == null)
                return;
            if (m_Childs != null) {
                for (int i = 0; i < m_Childs.Count; ++i) {
                    var node = m_Childs[i];
                    node.SubTreeContents(results);
                }
            }

            if (m_Contents != null) {
                results.AddRange(m_Contents);
            }
        }

        public void Query(Vector2 pos, List<T> results, bool isRetFirst = false) {
            if (results == null)
                return;

            if (m_Contents != null) {
                for (int i = 0; i < m_Contents.Count; ++i) {
                    var item = m_Contents[i];
                    if (item == null)
                        continue;
                    if (item.Rectangle.Contains(pos)) {
                        results.Add(item);
                        if (isRetFirst)
                            return;
                    }
                }
            }

            if (m_Childs != null) {
                for (int i = 0; i < m_Childs.Count; ++i) {
                    var node = m_Childs[i];
                    if (node.Bounds.Contains(pos)) {
                        node.Query(pos, results, isRetFirst);
                        if (isRetFirst && results.Count > 0)
                            return;
                    }
                }
            }
        }

        public void Query(Rect queryArea, List<T> results) {
            if (results == null)
                return;

            if (m_Contents != null) {
                for (int i = 0; i < m_Contents.Count; ++i) {
                    var item = m_Contents[i];
                    if (item == null)
                        continue;
                    if (queryArea.IntersectsWith(item.Rectangle))
                        results.Add(item);
                }
            }

            if (m_Childs != null) {
                for (int i = 0; i < m_Childs.Count; ++i) {
                    var node = m_Childs[i];
                    if (node.Bounds.IsContains(queryArea)) {
                        node.Query(queryArea, results);
                        break;
                    }

                    if (queryArea.IsContains(node.Bounds)) {
                        node.SubTreeContents(results);
                        continue;
                    }

                    if (node.Bounds.IntersectsWith(queryArea)) {
                        node.Query(queryArea, results);
                    }
                }
            }
        }

        private void CreateSubNodes(float limitWidthMulHeightSize = 10) {
            if (m_bounds.height * m_bounds.width <= limitWidthMulHeightSize) {
                return;
            }

            float halfWidth = (m_bounds.width / 2f);
            float halfHeight = (m_bounds.height / 2f);

            InitChilds();

            Vector2 min = m_bounds.min;
            Vector2 halfSz = new Vector2(halfWidth, halfHeight);
            m_Childs[0] = CreateQuadTreeNode(new Rect(m_bounds.position, halfSz));
            m_Childs[1] = CreateQuadTreeNode(new Rect(new Vector2(min.x, min.y + halfHeight), halfSz));
            m_Childs[2] = CreateQuadTreeNode(new Rect(new Vector2(min.x + halfWidth, min.y), halfSz));
            m_Childs[3] = CreateQuadTreeNode(new Rect(new Vector2(min.x + halfWidth, min.y + halfHeight), halfSz));
        }
    }

    // 四叉树
    public class QuadTree<T> where T: IHasRect {

        private QuadTreeNode<T> m_root = null;
        private Rect m_rectangle;

        private float m_LimitWidthMulHeightSize = 100;
        public float LimitWidthMulHeightSize {
            get {
                return m_LimitWidthMulHeightSize;
            }
        }

        public void Dispose() {
            if (m_root != null) {
                m_root.Dispose();
                m_root = null;
            }
        }

		public QuadTree(Rect rectangle, float limitWidthMulHeightSize = 100) {
            m_rectangle = rectangle;
			m_LimitWidthMulHeightSize = limitWidthMulHeightSize;
            m_root = QuadTreeNode<T>.CreateQuadTreeNode(m_rectangle);
        }

        public void Init(Rect rectangle, float limitWidthMulHeightSize = 100) {
            Dispose();
            m_LimitWidthMulHeightSize = limitWidthMulHeightSize;
            m_root = QuadTreeNode<T>.CreateQuadTreeNode(m_rectangle);
        }

        /// <summary>
        /// 获得Item数量，不要频繁调用，有性能问题
        /// </summary>
        public int Count {
            get {
                if (m_root == null)
                    return 0;
                return m_root.Count;
            }
        }

        public void RootNodeRemoveAllContentsAndChilds() {
            if (m_root != null) {
                m_root.RemoveAllContents();
                m_root.RemoveAllChilds();
            }
        }

        public bool Insert(T item) {
            if (m_root == null)
                return false;
            return m_root.Insert(item, this);
        }

        public bool ForEach(QuadTreeNode<T>.QTAction action, bool isFirstSelf = true) {
            if (m_root == null || action == null)
                return false;
            m_root.ForEach(action, isFirstSelf);
            return true;
        }

        public bool RemoveItem(T item, QuadTreeNode<T>.QTAction onCombineAction = null) {
            if (m_root == null)
                return false;
            return m_root.RemoveItem(item, onCombineAction);
        }

        public bool Query(Rect area, List<T> results) {
            if (m_root == null || results == null)
                return false;
            results.Clear();
            m_root.Query(area, results);
            return true;
        }

        public bool Query(Vector2 pos, List<T> results, bool isRetFirst = false) {
            if (m_root == null || results == null)
                return false;
            results.Clear();
            m_root.Query(pos, results, isRetFirst);
            return true;
        }
    }
}