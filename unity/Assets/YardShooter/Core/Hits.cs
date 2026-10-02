using UnityEngine;

namespace YardShooter
{
    public enum CoverType { Normal, Thin, Thick }

    // 태그 대신 컴포넌트를 쓰는 이유: 새 프로젝트에 TagManager 수정 없이 ThickCover/유리를 구분하기 위해.
    public class Cover : MonoBehaviour { public CoverType type; }

    public enum HitPart { Body, Head }

    public class Hitbox : MonoBehaviour
    {
        public Enemy owner;
        public HitPart part;
    }

    public static class Hits
    {
        // 플레이어는 Ignore Raycast(2) 레이어에 두어 자기 콜라이더에 레이가 막히지 않게 한다
        public const int PlayerLayer = 2;
        public static readonly int Mask = ~(1 << PlayerLayer);

        public static CoverType CoverOf(Collider c)
        {
            var cv = c.GetComponent<Cover>();
            return cv ? cv.type : CoverType.Normal;
        }

        public static float SegmentSegmentDistance(Vector3 p1, Vector3 q1, Vector3 p2, Vector3 q2)
        {
            Vector3 d1 = q1 - p1, d2 = q2 - p2, r = p1 - p2;
            float a = Vector3.Dot(d1, d1), e = Vector3.Dot(d2, d2), f = Vector3.Dot(d2, r);
            float c = Vector3.Dot(d1, r), b = Vector3.Dot(d1, d2), den = a * e - b * b;
            float s = den > 1e-8f ? Mathf.Clamp01((b * f - c * e) / den) : 0f;
            float t = (b * s + f) / e;
            if (t < 0) { t = 0; s = Mathf.Clamp01(-c / a); }
            else if (t > 1) { t = 1; s = Mathf.Clamp01((b - c) / a); }
            return Vector3.Distance(p1 + d1 * s, p2 + d2 * t);
        }

        public static Vector3 RandomCone(Vector3 dir, float deg)
        {
            if (deg <= 0) return dir;
            var q = Quaternion.LookRotation(dir);
            var c = Random.insideUnitCircle * Mathf.Tan(deg * Mathf.Deg2Rad);
            return (q * new Vector3(c.x, c.y, 1f)).normalized;
        }
    }
}
