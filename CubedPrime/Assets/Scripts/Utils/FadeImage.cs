
using UnityEngine;
using UnityEngine.UI;

public class FadeImage : MonoBehaviour
{
    // Start is called before the first frame update
    public Image fadeIMG;
    void Awake() {
        if(fadeIMG == null) GetComponent<Image>();
    }
}
