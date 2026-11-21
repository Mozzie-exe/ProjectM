using UnityEngine;

// Sinematik sonunda HouseLights kapanınca gaz lambalarının alev modelleri de söner.
// HouseLights her Build'de yeniden kurulduğu için bu script onun üstünde değil, ayrı bir objede durur
// ve HouseLights'ı adıyla bulur.
public class LampFlamesFollowHouseLights : MonoBehaviour
{
    public string houseLightsName = "HouseLights";
    public Renderer[] flames;   // vintage_oil_lamp_flame renderer'ları

    GameObject houseLights;

    void LateUpdate()
    {
        if (houseLights == null)
        {
            var house = transform.parent;
            var t = house != null ? house.Find(houseLightsName) : null;
            if (t != null) houseLights = t.gameObject;
            if (houseLights == null) return;
        }

        bool on = houseLights.activeInHierarchy;
        foreach (var f in flames)
            if (f != null && f.enabled != on) f.enabled = on;
    }
}
