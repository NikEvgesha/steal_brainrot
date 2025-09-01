using UnityEngine;

public class ItemData : ScriptableObject
{
    [SerializeField] private string _name;
    [SerializeField] private Sprite _img;
    /*    [SerializeField] private ItemType _type;
        [SerializeField] private bool _stackable;*/

    public string Name { get { return _name; } }
    public Sprite IMG { get { return _img; } }
    /*    public ItemType Type { get {return _type;} }
        public bool Stackable { get { return _stackable; } }*/
}
