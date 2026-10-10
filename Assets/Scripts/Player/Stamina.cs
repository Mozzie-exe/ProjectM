using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Stamina : MonoBehaviour
{
    public float stamina = 100f;
    public float mValue = 20f;
    public Slider StaminaBar;
    private float maxStamina;

    void Start()
    {
        maxStamina = stamina;

        if (StaminaBar != null)
        {
            StaminaBar.maxValue = maxStamina;
            StaminaBar.value = stamina;
        }
    }

    void Update()
    {
        if (Input.GetKey(KeyCode.LeftShift))
        {
            EnerjiAzalt();
        }
        else if (stamina < maxStamina)
        {
            EnerjiCogalt();
        }

        // Slider değerini her karede güncel tut
        if (StaminaBar != null)
        {
            StaminaBar.value = stamina;
        }
    }

    private void EnerjiAzalt()
    {
        stamina -= mValue * Time.deltaTime;
        if (stamina < 0f)
        {
            stamina = 0f;
        }
    }

    private void EnerjiCogalt()
    {
        stamina += mValue * Time.deltaTime;
        if (stamina > maxStamina)
        {
            stamina = maxStamina;
        }
    }
}