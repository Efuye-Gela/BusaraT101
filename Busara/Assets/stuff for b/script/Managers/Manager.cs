using Sirenix.OdinInspector;
using UnityEngine;

public abstract class Manager : SerializedMonoBehaviour
{
    public virtual void Initialize() { }
    public virtual void AfterInitialized() { }
}

public abstract class Manager<T> : Manager where T : Manager<T>
{
    static T instance;

    public static T Instance
    {
        get
        {
            if (instance == null)
                instance = FindFirstObjectByType<T>();

            return instance;
        }

        set
        {
            instance = value;
        }
    }

}
