using System.Collections.Generic;
using UnityEngine;

public abstract class Power : ScriptableObject
{

    public string powerName;

    [TextArea]
    public string powerDescription;

    public int virtueCost;
    public Power(string powerName, string powerDescription)
    {
        this.powerName = powerName;
        this.powerDescription = powerDescription;
    }

    public abstract bool IsValid(List<Virtue> virtue);
    public abstract void Execute();

}
