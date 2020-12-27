using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class kdTreeAddBuild : MonoBehaviour {

	void Start()
	{
		Renderer[] rs = GetComponentsInChildren<Renderer> ();
		if (rs != null && rs.Length > 0) {
			var clipper = ObjectGPUInstancingMgr.GetInstance ().Clipper;
			if (clipper != null) {
				clipper.ReBuild (rs, true);
				//ObjectGPUInstancingMgr.GetInstance ().UpdateClipper ();
			} else {
				clipper = GameObject.FindObjectOfType<KdTreeCameraClipper> ();
				if (clipper != null)
					clipper.ReBuild (rs, false);
			}
		}
	}
}
