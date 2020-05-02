﻿using UnityEngine;
using UnityEngine.UI;

public class TotalCapacity : MonoBehaviour
{
    private Inventory _inventory;
    private Text _text;
    
    void Start()
    {
        _inventory = GameObject.Find("Inventory").GetComponent<Inventory>();
        _text = GetComponent<Text>();
    }

    void Update()
    {
        _text.text = _inventory.GetTotalCapacity().ToString();
    }
}