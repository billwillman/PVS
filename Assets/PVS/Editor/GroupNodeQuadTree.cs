using System;
using UnityEngine;
using NsLib.Utils;
using System.Collections.Generic;

// 只在编辑器下用，不需要考虑性能问题
namespace NsLib.PVS.Editor {

    public class QuadGroupNodeArray<T>: PoolNode<QuadGroupNodeArray<T>> where T: class
    {
        private QuadGroupNode<T>[] m_Items = new QuadGroupNode<T>[4];
 

        protected override void OnFree() {
            base.OnFree();
            for (int i = 0; i < m_Items.Length; ++i) {
                var item = m_Items[i];
                if (item != null) {
                    item.Dispose();
                    m_Items[i] = null;
                }
            }
        }

        public int Count {
            get {
                if (m_Items == null)
                    return 0;
                return m_Items.Length;
            }
        }

        public QuadGroupNode<T> this[int idx] {
            get {
                if (idx < 0 || idx >= Count)
                    return null;
                return m_Items[idx];
            }

            set {
                if (idx < 0 || idx >= Count)
                    return;
                m_Items[idx] = value;
            }
        }
    }

    public class QuadGroupNode<T> : PoolNode<QuadGroupNode<T>> where T : class {
        private System.Object m_Item = null;

        public QuadGroupNode() {
        }

        public int LeafCount {
            get {
                int ret = 0;
                if (m_Item != null) {
                    var array = m_Item as QuadGroupNodeArray<T>;
                    if (array != null) {
                        for (int i = 0; i < array.Count; ++i) {
                            var iter = array[i];
                            if (iter != null)
                                ret += iter.LeafCount;
                        }
                    } else {
                        if (m_Item is T) {
                            ret = 1;
                        }
                    }
                }
                return ret;
            }
        }

		public void ToList(List<T> list)
		{
			if (list == null)
				return;
			var array = this.ItemNode;
			if (array != null) {
				for (int i = 0; i < array.Count; ++i) {
					var n = array [i];
					if (n == null)
						continue;
					n.ToList (list);
				}
			} else {
				var item = this.Item;
				if (item != null)
					list.Add (item);
			}
		}

        protected override void OnFree() {
            base.OnFree();

            QuadGroupNodeArray<T> array = m_Item as QuadGroupNodeArray<T>;
            if (array != null) {
                array.Dispose();
            }
            m_Item = null;
        }

        public void Init(T cell) {
            m_Item = cell;
        }

        public void Init(QuadGroupNodeArray<T> array) {
            m_Item = array;
        }

        // 是否是叶子节点（终节点）
        public bool IsLeafNode {
            get {
                bool ret = (m_Item == null) || (!(m_Item is QuadGroupNodeArray<T>));
                return ret;
            }
        }

        public T Item {
            get {
                return (m_Item as T);
            }
        }

        public QuadGroupNodeArray<T> ItemNode {
            get {
                return (m_Item as QuadGroupNodeArray<T>);
            }
        }

		private static bool Combine(ref System.Object m_Item, QuadGroupNodeArray<T> array, Func<T, T, T, T, bool> onCompare, Func<T, T, T, T, T> onCreateCombine)
		{
			if (array == null || onCompare == null || onCreateCombine == null)
				return false;
			bool ret = false;

			var n1 = array [0];
			var n2 = array [1];
			var n3 = array [2];
			var n4 = array [3];

			if (n1 != null && n2 != null && n3 != null && n4 != null) {
				var item1 = n1.Item;
				var item2 = n2.Item;
				var item3 = n3.Item;
				var item4 = n4.Item;
				if (item1 != null && item2 != null && item3 != null && item4 != null) {
					ret = onCompare (item1, item2, item3, item4);
					if (ret) {
						T newT = onCreateCombine (item1, item2, item3, item4);
						array.Dispose ();
						m_Item = newT;
					}
				} else {
					var array1 = n1.ItemNode;
					var array2 = n2.ItemNode;
					var array3 = n3.ItemNode;
					var array4 = n4.ItemNode;

					if (array1 != null && array2 != null && array3 != null && array4 != null) {
						bool r1 = Combine (ref n1.m_Item, array1, onCompare, onCreateCombine);
						bool r2 = Combine (ref n2.m_Item, array2, onCompare, onCreateCombine);
						bool r3 = Combine (ref n3.m_Item, array3, onCompare, onCreateCombine);
						bool r4 = Combine (ref n4.m_Item, array4, onCompare, onCreateCombine);
						ret = r1 || r2 || r3 || r4;
					}
				}
			} else {
				if (n1 != null) {
					bool r1 = n1.Combine (onCompare, onCreateCombine);
					if (r1 && !ret)
						ret = true;
				}

				if (n2 != null) {
					bool r2 = n2.Combine (onCompare, onCreateCombine);
					if (r2 && !ret)
						ret = true;
				}

				if (n3 != null) {
					bool r3 = n3.Combine (onCompare, onCreateCombine);
					if (r3 && !ret)
						ret = true;
				}

				if (n4 != null) {
					bool r4 = n4.Combine (onCompare, onCreateCombine);
					if (r4 && !ret)
						ret = true;
				}
			}


			return ret;
		}

		internal bool Combine(Func<T, T, T, T, bool> onCompare, Func<T, T, T, T, T> onCreateCombine)
		{
			if (onCompare == null || onCompare == null)
				return false;
			var array = this.ItemNode;
			if (array != null) {
				var n1 = array [0];
				var n2 = array [1];
				var n3 = array [2];
				var n4 = array [3];
				if (n1 != null && n2 != null && n3 != null && n4 != null) {
					var item1 = n1.Item;
					var item2 = n2.Item;
					var item3 = n3.Item;
					var item4 = n4.Item;
					if (item1 != null && item2 != null && item3 != null && item4 != null) {
						bool ret = onCompare (item1, item2, item3, item4);
						if (ret) {
							T newT = onCreateCombine (item1, item2, item3, item4);
							array.Dispose ();
							m_Item = newT;
						}
						return ret;
					} else {
						var array1 = n1.ItemNode;
						var array2 = n2.ItemNode;
						var array3 = n3.ItemNode;
						var array4 = n4.ItemNode;

						if (array1 != null && array2 != null && array3 != null && array4 != null) {
							bool r1 = Combine (ref n1.m_Item, array1, onCompare, onCreateCombine);
							bool r2 = Combine (ref n2.m_Item, array2, onCompare, onCreateCombine);
							bool r3 = Combine (ref n3.m_Item, array3, onCompare, onCreateCombine);
							bool r4 = Combine (ref n4.m_Item, array4, onCompare, onCreateCombine);
							return r1 || r2 || r3 || r4;
						} else
							return false;
					}
				} else {
					bool ret = false;
					if (n1 != null) {
						bool r1 = n1.Combine (onCompare, onCreateCombine);
						if (r1 && !ret)
							ret = true;
					}

					if (n2 != null) {
						bool r2 = n2.Combine (onCompare, onCreateCombine);
						if (r2 && !ret)
							ret = true;
					}

					if (n3 != null) {
						bool r3 = n3.Combine (onCompare, onCreateCombine);
						if (r3 && !ret)
							ret = true;
					}

					if (n4 != null) {
						bool r4 = n4.Combine (onCompare, onCreateCombine);
						if (r4 && !ret)
							ret = true;
					}

					return ret;
				}
			} else
				return false;
		}
    }


    public class QuadGroupTree<T> where T : class {

        private static readonly int _cPer = 2;
        private QuadGroupNode<T> m_Root = null;

        public int LeafCount {
            get {
                if (m_Root == null)
                    return 0;
                return m_Root.LeafCount;
            }
        }

        public void Dispose() {
            if (m_Root != null) {
                m_Root.Dispose();
                m_Root = null;
            }
        }

        public QuadGroupTree(int row, int col, T[] array) {
            m_Root = InitTree(row, col, array);
        }

        private void InitTree(ref int row, ref int col, ref List<QuadGroupNode<T>> parentNodeList) {

            if (parentNodeList == null || parentNodeList.Count <= 0)
                return;


            int rCnt = Mathf.CeilToInt(((float)row) / ((float)_cPer));
            int cCnt = Mathf.CeilToInt(((float)col) / ((float)_cPer));

            var pNodeList = parentNodeList;
            Func<int, QuadGroupNode<T>> GetItem = (int idx) =>
            {
                if (idx < 0 || idx >= pNodeList.Count)
                    return null;
                return pNodeList[idx];
            };

            List<QuadGroupNode<T>> nodeList = new List<QuadGroupNode<T>>();
            for (int r = 0; r < rCnt; ++r) {
                for (int c = 0; c < cCnt; ++c) {

                    /*
                     * 3 4
                     * 1 2
                     */

                    int rr = r * _cPer;
                    int cc = c * _cPer;
                    int idx1 = (rr) * col + cc;
                    int idx2 = -1;
                    bool isVaildCC = cc + 1 < col;
                    if (isVaildCC)
                        idx2 = (rr) * col + cc + 1;

                    int idx3 = -1;
                    bool isVaildrr = rr + 1 < row;
                    if (isVaildrr)
                        idx3 = (rr + 1) * col + cc;
                    int idx4 = -1;
                    if (isVaildCC && isVaildrr)
                        idx4 = (rr + 1) * col + cc + 1;

                    QuadGroupNode<T> root = AbstractPool<QuadGroupNode<T>>.GetNode() as QuadGroupNode<T>;

                    var n1 = GetItem(idx1);
                    var n2 = GetItem(idx2);
                    var n3 = GetItem(idx3);
                    var n4 = GetItem(idx4);

                    QuadGroupNodeArray<T> nodeArray = AbstractPool<QuadGroupNodeArray<T>>.GetNode() as QuadGroupNodeArray<T>;
                    nodeArray[0] = n1;
                    nodeArray[1] = n2;
                    nodeArray[2] = n3;
                    nodeArray[3] = n4;

                    root.Init(nodeArray);
                    nodeList.Add(root);
                }
            }



            row = rCnt;
            col = cCnt;
            parentNodeList = nodeList;
        }

        private QuadGroupNode<T> InitTree(int row, int col, T[] array) {
            if (row <= 0 || col <= 0 || array == null || array.Length <= 0)
                return null;
           
            int rCnt = Mathf.CeilToInt( ((float)row) / ((float)_cPer));
            int cCnt = Mathf.CeilToInt(((float)col) / ((float)_cPer));

            Func<int, T> GetItem = (int idx) =>
            {
                if (idx < 0 || idx >= array.Length)
                    return null;
                return array[idx];
            };

            List<QuadGroupNode<T>> nodeList = new List<QuadGroupNode<T>>();

            for (int r = 0; r < rCnt; ++r) {
                for (int c = 0; c < cCnt; ++c) {

                    /*
                     * 3 4
                     * 1 2
                     */

                    int rr = r * _cPer;
                    int cc = c * _cPer;
                    int idx1 = (rr) * col + cc;
                    int idx2 = -1;
                    bool isVaildCC = cc + 1 < col;
                    if (isVaildCC)
                        idx2 = (rr) * col + cc + 1;

                    int idx3 = -1;
                    bool isVaildrr = rr + 1 < row;
                    if (isVaildrr)
                        idx3 = (rr + 1) * col + cc;
                    int idx4 = -1;
                    if (isVaildCC && isVaildrr)
                        idx4 = (rr + 1) * col + cc + 1;

                    QuadGroupNode<T> root = AbstractPool<QuadGroupNode<T>>.GetNode() as QuadGroupNode<T>;

                    QuadGroupNode<T> n1 = AbstractPool<QuadGroupNode<T>>.GetNode() as QuadGroupNode<T>;
                    QuadGroupNode<T> n2 = AbstractPool<QuadGroupNode<T>>.GetNode() as QuadGroupNode<T>;
                    QuadGroupNode<T> n3 = AbstractPool<QuadGroupNode<T>>.GetNode() as QuadGroupNode<T>;
                    QuadGroupNode<T> n4 = AbstractPool<QuadGroupNode<T>>.GetNode() as QuadGroupNode<T>;

                    T item1 = GetItem(idx1);
                    T item2 = GetItem(idx2);
                    T item3 = GetItem(idx3);
                    T item4 = GetItem(idx4);

                    n1.Init(item1);
                    n2.Init(item2);
                    n3.Init(item3);
                    n4.Init(item4);

                    QuadGroupNodeArray<T> nodeArray = AbstractPool<QuadGroupNodeArray<T>>.GetNode() as QuadGroupNodeArray<T>;
                    nodeArray[0] = n1;
                    nodeArray[1] = n2;
                    nodeArray[2] = n3;
                    nodeArray[3] = n4;

                    root.Init(nodeArray);
                    nodeList.Add(root);
                }
            }

            while (true) {
                InitTree(ref rCnt, ref cCnt, ref nodeList);
                if (nodeList.Count <= 1)
                    break;
            }

            if (nodeList.Count <= 0)
                return null;

            return nodeList[0];
        }

		public bool Combine(Func<T, T, T, T, bool> onCompare, Func<T, T, T, T, T> onCreateCombine)
		{
			if (onCompare == null || onCreateCombine == null || m_Root == null)
				return false;
			return m_Root.Combine (onCompare, onCreateCombine);
		}

		public List<T> ToList()
		{
			List<T> ret = null;
			if (m_Root == null)
				return ret;
			ret = new List<T> ();
			m_Root.ToList (ret);
			return ret;
		}

    }

}
