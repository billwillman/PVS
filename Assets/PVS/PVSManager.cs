using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using System.Runtime.InteropServices;
using NsLib.NsTcpClient;
using NsLib.Utils;

namespace NsLib.PVS {

	internal enum PVSSaveMode
	{
		bitsArrayMode = 0,
		bitsCellMode
	}

    // 初始是按照10x10米一个CELL, 必然是CELL的倍数，合并也按照四叉树一样合并
    public class PVSCell: IHasRect
        {
        // 位置
        public Vector3 position;
        public byte lod; // lod = 0 表示10x10 (最小是10米x10米)
        public byte[] visibleBits = null; // 可见数组bit位

        public Rect Rectangle {
            get {
                Rect ret = new Rect();
				float sz = ((float)(Mathf.Pow(2, lod))) * CellSize;
                ret.size = new Vector2(sz, sz);
				ret.center = new Vector2(position.x, position.z);
                return ret;
            }
        }

		public float LodSize
		{
			get
			{
				return GetLodSize (lod);	
			}
		}

		public Bounds GetBounds(float camY = 0)
		{
			float sz = this.LodSize;
			Vector3 size = new Vector3 (sz, camY, sz);
			Bounds ret = new Bounds (this.position, size);
			return ret;
		}

        public static float GetLodSize(byte lod) {
            float ret = ((float)(Mathf.Pow(2.0f, lod))) * CellSize;
            return ret;
        }

#if UNITY_EDITOR
        public bool isEditorDone = false;
        public int visibleBitsIdx = -1;

        public int CheckCompare(PVSCell other) {
            if (other == null)
                return -1;
            if (other.visibleBits == null && this.visibleBits == null)
                return 0;

            if (other.visibleBits == null)
                return -1;
            if (this.visibleBits == null)
                return 1;

            int ret = visibleBits.Length - other.visibleBits.Length;
            if (ret != 0)
                return ret;

            for (int i = 0; i < visibleBits.Length; ++i) {
                var b1 = visibleBits[i];
                var b2 = other.visibleBits[i];
                ret = b1 - b2;
                if (ret != 0)
                    return ret;
            }

            return ret;
        }

        public bool CompareVisibleBits(PVSCell other) {
            if (other == null)
                return false;
            if (visibleBits == null)
                return false;
            if (visibleBits.Length != other.visibleBits.Length)
                return false;
            for (int i = 0; i < visibleBits.Length; ++i) {
                var b1 = visibleBits[i];
                var b2 = other.visibleBits[i];
                if (b1 != b2)
                    return false;
            }

            return true;
        }

        public bool CompareVisibleBits(byte[] other) {
            if (other == null)
                return false;
            if (visibleBits == null)
                return false;
            if (visibleBits.Length != other.Length)
                return false;

            for (int i = 0; i < visibleBits.Length; ++i) {
                var b1 = visibleBits[i];
                var b2 = other[i];
                if (b1 != b2)
                    return false;
            }

            return true;
        }

#endif

        // 默认不存在列表的处理方式
        public static readonly bool NoContainsVisible = true;
        public static readonly float CellSize = 10f;

        // 是否可见
        public bool IsVisible(int id) {
            if (visibleBits == null)
                return NoContainsVisible;
			int idx, bitIdx;
			if (!GetBitsIdx (id, out idx, out bitIdx))
				return NoContainsVisible;
            byte b = visibleBits[idx];
			bool ret = (((byte)(1 << bitIdx)) & b) != 0;
            return ret;
        }

        public void ResetVisible() {
            if (visibleBits != null) {
                for (int i = 0; i < visibleBits.Length; ++i) {
                    visibleBits[i] = 0;
                }
            }
        }

        private bool GetBitsIdx(int id, out int idx, out int bit) {
            --id;
            idx = (int)(id) / 8;
            bit = -1;
            if (idx < 0 || idx >= visibleBits.Length)
                return false;
            bit = id - idx * 8;
            if (bit < 0 || bit >= 8)
                return false;
            return true;
        }

        // 当前Instance对象是否可见（InstanceID来自MeshRenderer）
        public bool IsInstanceIDVisible(int instanceID) {
            int id;
            if (!PVSManager.GetInstance().SearchID(instanceID, out id))
                return NoContainsVisible;
            return IsVisible(id);
        }

        public void SetVisible(int id, bool isVisible) {
            int idx, bit;
            bool ret = GetBitsIdx(id, out idx, out bit);
            if (!ret)
                return;
            var b = visibleBits[idx];
            if (isVisible) {
                b |= (byte)(1 << bit);
            } else {
                b = (byte)(b & ~(1 << bit));
            }
            visibleBits[idx] = b;
        }

		internal unsafe void LoadFromStream(Stream stream, List<byte[]> byteArrayList, PVSSaveMode saveMode, int bitLen) {
            if (stream == null)
                return;

            // 读取位置
            PVSManager.ReadFromStream(stream, out this.position);
            // 读取LOD数据
            PVSManager.ReadFromStream(stream, out this.lod);
            // 可见bit位
			if (saveMode == PVSSaveMode.bitsArrayMode)
			{
            	int idx;
            	PVSManager.ReadFromStream(stream, out idx);
            	this.visibleBits = byteArrayList[idx];
			} else if (saveMode == PVSSaveMode.bitsCellMode)
			{
				this.visibleBits = new byte[bitLen];
				stream.Read(this.visibleBits, 0, bitLen);
			}
        }

#if UNITY_EDITOR

		internal int ItemSize(PVSSaveMode saveMode) {
			int ret = Marshal.SizeOf(this.position);
			ret += Marshal.SizeOf(this.lod);
			if (saveMode == PVSSaveMode.bitsArrayMode)
				ret += Marshal.SizeOf (this.visibleBitsIdx);
			else if (saveMode == PVSSaveMode.bitsCellMode)
				ret += this.visibleBits.Length * Marshal.SizeOf (typeof(byte));
			return ret;
        }

		internal unsafe void SaveToStream(Stream stream, PVSSaveMode saveMode) {
            if (stream == null)
                return;

            PVSManager.WriteStream(stream, this.position);
            PVSManager.WriteStream(stream, this.lod);
			if (saveMode == PVSSaveMode.bitsArrayMode)
            	PVSManager.WriteStream(stream, this.visibleBitsIdx);
			else if (saveMode == PVSSaveMode.bitsCellMode)
				stream.Write(this.visibleBits, 0, this.visibleBits.Length * Marshal.SizeOf (typeof(byte)));
        }
#endif
    }

    // PVS节点
    internal struct PVSNode {
        public int id;
        public int instanceID;
    }

    internal struct PVSNodeHeader {
		public byte saveMode;
        // 格子数量
        public int cellCount;
        // bit数据数量
        public int bitsArrayLength;
        public Vector2 quadTreeCenter;
        public Vector2 quadTreeSize;
    }

    public class PVSManager : Singleton<PVSManager> {

        internal unsafe static void ReadFromStream(Stream stream, out Vector3 ret) {
            ByteBufferNode node = NetByteArrayPool.GetByteBufferNode(Marshal.SizeOf(typeof(Vector3)));
            try {
                stream.Read(node.Buffer, 0, node.DataSize);
                Vector3 p;
                System.IntPtr dstPtr = (System.IntPtr)(&p);
                Marshal.Copy(node.Buffer, 0, dstPtr, node.DataSize);
                ret = p;
            } finally {
                node.Dispose();
            }
        }

        internal unsafe static void ReadFromStream(Stream stream, out int ret) {
            int v1 = stream.ReadByte();
            int v2 = stream.ReadByte();
            int v3 = stream.ReadByte();
            int v4 = stream.ReadByte();

            ret = (v4 << 24) | (v3 << 16) | (v2 << 8) | v1;
        }

        internal unsafe static void ReadFromStream(Stream stream, out byte ret) {
            ret = (byte)stream.ReadByte();
        }

        internal unsafe static void ReadFromStream(Stream stream, out byte[] buffer) {
            int cnt;
            ReadFromStream(stream, out cnt);
            buffer = new byte[cnt];
            stream.Read(buffer, 0, cnt);
        }

        internal unsafe static void ReadFromStream(Stream stream, out PVSNodeHeader ret) {
            ByteBufferNode node = NetByteArrayPool.GetByteBufferNode(Marshal.SizeOf(typeof(PVSNodeHeader)));
            try {
                stream.Read(node.Buffer, 0, node.DataSize);
                PVSNodeHeader p;
                System.IntPtr dstPtr = (System.IntPtr)(&p);
                Marshal.Copy(node.Buffer, 0, dstPtr, node.DataSize);
                ret = p;
            } finally {
                node.Dispose();
            }
        }

        internal unsafe static void ReadFromStream(Stream stream, out PVSNode ret) {
            ByteBufferNode node = NetByteArrayPool.GetByteBufferNode(Marshal.SizeOf(typeof(PVSNode)));
            try {
                stream.Read(node.Buffer, 0, node.DataSize);
                PVSNode p;
                System.IntPtr dstPtr = (System.IntPtr)(&p);
                Marshal.Copy(node.Buffer, 0, dstPtr, node.DataSize);
                ret = p;
            } finally {
                node.Dispose();
            }
        }

        // Instance, ID
        private Dictionary<int, int> m_InstanceIDToIDMap = null;
        private QuadTree<PVSCell> m_CellTree = null;
        private List<PVSCell> m_CellSearchResults = null;

        public void RegisterID(int id, Transform target) {
            if (target == null)
                return;
            if (m_InstanceIDToIDMap == null)
                m_InstanceIDToIDMap = new Dictionary<int, int>();

            m_InstanceIDToIDMap[target.GetInstanceID()] = id;
        }

        // 从物件instanceID查询PVS分配的ID
        public bool SearchID(int instanceID, out int id) {
            if (m_InstanceIDToIDMap == null) {
                id = 0;
                return false;
            }
            bool ret = m_InstanceIDToIDMap.TryGetValue(instanceID, out id);
            return ret;
        }

        public void Clear(bool isClearIDMap = true) {
            if (isClearIDMap  && m_InstanceIDToIDMap != null)
                m_InstanceIDToIDMap.Clear();
            if (m_CellTree != null)
                m_CellTree.Dispose();
            if (m_CellSearchResults != null)
                m_CellSearchResults.Clear();
        }

#if UNITY_EDITOR

        internal static unsafe void WriteStream(Stream stream, Vector3 v) {
            var byteBufferNode = NetByteArrayPool.GetByteBufferNode(Marshal.SizeOf(v));
            try {
                System.IntPtr srcPtr = (System.IntPtr)(&v);
                Marshal.Copy(srcPtr, byteBufferNode.Buffer, 0, byteBufferNode.DataSize);
                stream.Write(byteBufferNode.Buffer, 0, byteBufferNode.DataSize);
            } finally {
                byteBufferNode.Dispose();
            }
        }

       internal static unsafe void WriteStream(Stream stream, byte v) {
            stream.WriteByte(v);
        }

        internal static unsafe void WriteStream(Stream stream, int v) {
            byte b1 = (byte)v;
            byte b2 = (byte)(v >> 8);
            byte b3 = (byte)(v >> 16);
            byte b4 = (byte)(v >> 24);
            stream.WriteByte(b1);
            stream.WriteByte(b2);
            stream.WriteByte(b3);
            stream.WriteByte(b4);
        }

        internal unsafe static void WriteStream(Stream stream, PVSNodeHeader header) {
            var byteBufferNode = NetByteArrayPool.GetByteBufferNode(Marshal.SizeOf(header));
            try {
                System.IntPtr srcPtr = (System.IntPtr)(&header);
                Marshal.Copy(srcPtr, byteBufferNode.Buffer, 0, byteBufferNode.DataSize);
                stream.Write(byteBufferNode.Buffer, 0, byteBufferNode.DataSize);
            } finally {
                byteBufferNode.Dispose();
            }
        }

		public unsafe static void SaveToStream(Stream stream, PVSCell[] cellResults, Rect quadTreeRect, int customSaveMode = -1) {
            if (cellResults == null || cellResults.Length <= 0)
                return;

            PVSNodeHeader header = new PVSNodeHeader();
            header.quadTreeSize = quadTreeRect.size;
            header.quadTreeCenter = quadTreeRect.center;

            // 合并一样的bit数组,节省空间
            int cellCnt = 0;
            List<byte[]> byteArrList = new List<byte[]>();
            for (int i = 0; i < cellResults.Length; ++i) {
                var cell = cellResults[i];
                if (cell == null || cell.visibleBits == null)
                    continue;
                ++cellCnt;
                bool isFound = false;
                for (int j = 0; j < byteArrList.Count; ++j) {
                    var oldCell = byteArrList[j];
                    if (oldCell == null)
                        continue;
                    if (cell.CompareVisibleBits(oldCell)) {
                        cell.visibleBits = oldCell;
                        cell.visibleBitsIdx = j;
                        isFound = true;
                        break;
                    }
                }
                if (!isFound) {
                    cell.visibleBitsIdx = byteArrList.Count;
                    byteArrList.Add(cell.visibleBits);
                }
            }

            header.bitsArrayLength = byteArrList.Count;

            header.cellCount = cellCnt;

			float bitsSize = 0f;
			if (cellResults.Length > 0 && cellResults[0].visibleBits != null)
			{
				bitsSize = (cellResults[0].visibleBits.Length * Marshal.SizeOf(typeof(byte)))/1024f;
			}

			float bitsTotualSize = byteArrList.Count * bitsSize;

			PVSSaveMode saveMode = PVSSaveMode.bitsArrayMode;
			float revertSize = header.cellCount * bitsSize - ((header.cellCount * Marshal.SizeOf(typeof(int)))/1024f + bitsTotualSize);
			if (revertSize < 0)
				saveMode = PVSSaveMode.bitsCellMode;

			if (customSaveMode >= 0)
				saveMode = (PVSSaveMode)customSaveMode;

			header.saveMode = (byte)saveMode;



            // 1.写入文件头
            WriteStream(stream, header);


			if (saveMode == PVSSaveMode.bitsArrayMode)
			{
            	// 2.写入byteArray
            	for (int i = 0; i < byteArrList.Count; ++i) {
                	var bs = byteArrList[i];
                	stream.Write(bs, 0, bs.Length);
            	}
			}	

            // 3.写入Cell信息
            float cellTotualSize = 0;
            for (int i = 0; i < cellResults.Length; ++i) {
                var cell = cellResults[i];
                if (cell == null)
                    continue;
				cell.SaveToStream(stream, saveMode);
				cellTotualSize += cell.ItemSize(saveMode);
            }
            cellTotualSize /= 1024f;


			Debug.LogFormat("Bits数组数量：{0:D}个（占用大小：{1}K） 场景CELL数量:{2:D}个（占用大小：{3}K）[节省：{4}K, 存储模式：{5}]", 
				header.bitsArrayLength, bitsTotualSize.ToString(), header.cellCount, cellTotualSize.ToString(), revertSize.ToString(), saveMode.ToString());
        }

        private static int OnPVSCellArraySort(PVSCell c1, PVSCell c2) {
            if (c1 == null && c2 == null) {
                return 0;
            }
            if (c1 == null)
                return 1;
            if (c2 == null)
                return -1;
            int ret = c1.CheckCompare(c2);
            return ret;
        }

#endif

        /// <summary>
        /// 根据位置和半径搜索CELL
        /// </summary>
        /// <param name="position">位置</param>
        /// <param name="radius">半径</param>
        /// <returns></returns>

        public List<PVSCell> SearchCell(Vector2 position) {
            if (m_CellTree == null)
                return null;
            if (m_CellSearchResults == null)
                m_CellSearchResults = new List<PVSCell>();
            else
                m_CellSearchResults.Clear();
            m_CellTree.Query(position, m_CellSearchResults, true);
            return m_CellSearchResults;
        }

        public unsafe void LoadFromStream(Stream stream) {

            Clear(false);

            if (stream == null)
                return;

            PVSNodeHeader header;
            ReadFromStream(stream, out header);

			int bitLen = 0;
			if (m_InstanceIDToIDMap != null && m_InstanceIDToIDMap.Count > 0)
			{
				bitLen = Mathf.CeilToInt(((float)m_InstanceIDToIDMap.Count) / 8.0f);
			}

            // 1.读取visibleBitArray
            List<byte[]> visibleArrayList = null;
			if (header.saveMode == (byte)PVSSaveMode.bitsArrayMode)
			{
           	 	if (m_InstanceIDToIDMap != null && m_InstanceIDToIDMap.Count > 0) {
                	visibleArrayList = new List<byte[]>(header.bitsArrayLength);
                	for (int i = 0; i < header.bitsArrayLength; ++i) {
						byte[] array = new byte[bitLen];
						stream.Read(array, 0, bitLen);
                    	visibleArrayList.Add(array);
                	}
            	}
			}

            // 2.读取Cell
			if ((header.saveMode == (byte)PVSSaveMode.bitsCellMode)|| (visibleArrayList != null && visibleArrayList.Count > 0)) {

                Rect quadTreeRect = new Rect();
                quadTreeRect.size = header.quadTreeSize;
                quadTreeRect.center = header.quadTreeCenter;
                if (m_CellTree == null) {   
                    m_CellTree = new QuadTree<PVSCell>(quadTreeRect, PVSCell.CellSize * PVSCell.CellSize);
                } else {
                    m_CellTree.Init(quadTreeRect, PVSCell.CellSize * PVSCell.CellSize);
                }

                for (int i = 0; i < header.cellCount; ++i) {
                    var cell = new PVSCell();
					cell.LoadFromStream(stream, visibleArrayList, (PVSSaveMode)header.saveMode, bitLen);
                    m_CellTree.Insert(cell);
                }

#if UNITY_EDITOR
                Debug.LogFormat("header Cell Count: {0:D}  Loaded Cell Count: {1:D}", header.cellCount, m_CellTree.Count);
#endif
            }

        }

        public void UpdateViewer(IPVSViewer viewer, Action<PVSCell> onViewObjChged) {
            if (viewer == null)
                return;
            Vector2 pos = viewer.ViewPos;
            PVSCell currCell = viewer._CurrentCell;
            if (currCell != null) {
                if (currCell.Rectangle.Contains(pos))
                    return;
            }

            var results = SearchCell(pos);
            if (results != null && results.Count > 0) {
                var newCell = results[0];
                if (newCell != currCell) {
                    viewer._CurrentCell = newCell;
                    if (onViewObjChged != null)
                        onViewObjChged(newCell);
                }
            }
        }
    }

    // PVS观察者
    public interface IPVSViewer {
        // 站在的当前格子
        PVSCell _CurrentCell {
            get;
            set;
        }

        /// <summary>
        /// XZ的世界坐标
        /// </summary>
        Vector2 ViewPos {
            get;
        }
    }

}
