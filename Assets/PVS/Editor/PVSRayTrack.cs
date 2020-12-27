using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NsLib.PVS {

    public static class PVSRayTrack {
        private static float sdBox(Vector3 pt, Vector3 boundSize) {

            // SDF是半宽半高
            boundSize /= 2.0f;

            pt.x = Mathf.Abs(pt.x);
            pt.y = Mathf.Abs(pt.y);
            pt.z = Mathf.Abs(pt.z);
            Vector3 q = pt - boundSize;

            Vector3 q1 = new Vector3();
            q1.x = Mathf.Max(q.x, 0f);
            q1.y = Mathf.Max(q.y, 0f);
            q1.z = Mathf.Max(q.z, 0f);

            float ret = q1.magnitude + Mathf.Min( Mathf.Max(q.x, Mathf.Max(q.y, q.z)), 0f);
            return ret;
        }

        internal static float sdPVSCell(Vector3 pt, PVSBounds bounds) {
            Matrix4x4 mat = Matrix4x4.identity;

            Vector3 center = bounds.center;
            Vector3 size = bounds.size;
            Vector3 up = bounds.up;
            Vector3 zz = bounds.forward;
            Vector3 right = Vector3.Cross(up, zz);

            mat.m00 = right.x; mat.m01 = right.y; mat.m02 = right.z;
            mat.m10 = up.x; mat.m11 = up.y; mat.m12 = up.z;
            mat.m20 = zz.x; mat.m21 = zz.y; mat.m22 = zz.z;

            pt -= center;

            pt = mat.MultiplyPoint(pt);

            float ret = sdBox(pt, size);
            return ret;
        }

        internal static bool sdPVSCell(ref Ray ray, PVSBounds bounds, ref float tutDistance) {
            float distance = sdPVSCell(ray.origin, bounds);
            if (distance <= Vector3.kEpsilon)
                return true;
            tutDistance += distance;
            ray.origin += distance * ray.direction;

            return false;
        }

        internal static int sdPVSCells(Vector3 pt, PVSBounds[] bounds, out float distance) {
            distance = float.MaxValue;
            if (bounds == null || bounds.Length <= 0)
                return -1;
            for (int i = 0; i < bounds.Length; ++i) {
                var b = bounds[i];
                float dist = sdPVSCell(pt, b);
                distance = Mathf.Min(distance, dist);
                if (dist <= Vector3.kEpsilon)
                    return i;
            }
            return -1;
        }

        internal static int sdPVSCells(ref Ray ray, PVSBounds[] bounds,ref float tutDistance) {
            float distance;
            int idx = sdPVSCells(ray.origin, bounds, out distance);
            if (idx >= 0)
                return idx;
            ray.origin += ray.direction * distance;
            tutDistance += distance;
            return -1;
        }
    }
}
