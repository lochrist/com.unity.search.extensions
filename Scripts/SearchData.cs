using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SearchData : MonoBehaviour
{
    public enum SearchChoice
    {
      Yes,
      No,
      Toaster
    }

    public string text;
    public Color color;
    public float number;
    public SearchChoice choice;
    public bool valid;    
}
