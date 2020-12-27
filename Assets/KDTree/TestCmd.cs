using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class TestCmd : MonoBehaviour {

	public MeshRenderer renderer = null;
	private CommandBuffer m_Cmd = null;

	// Use this for initialization
	void Start () {
		m_Cam = GetComponent<Camera> ();
		MeshFilter filter = renderer.GetComponent<MeshFilter> ();

		Matrix4x4[] mat = new Matrix4x4[1];
		mat [0] = Matrix4x4.TRS (renderer.transform.position, renderer.transform.rotation, renderer.transform.lossyScale);
		m_Cmd = new CommandBuffer ();
		m_Cmd.DrawMeshInstanced (filter.sharedMesh, 0, renderer.sharedMaterial, 0, mat);
		m_Cam.AddCommandBuffer (CameraEvent.AfterForwardOpaque, m_Cmd);
	}

	void OnDestroy()
	{
		Clear ();
	}

	void OnApplicationQuit()
	{
		Clear ();
	}

	void Clear()
	{
		if (m_Cmd != null) {
			m_Cmd.Dispose ();
			m_Cmd = null;
		}
	}
	
	private Camera m_Cam;
}
