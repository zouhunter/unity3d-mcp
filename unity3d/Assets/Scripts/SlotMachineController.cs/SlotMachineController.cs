using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class SlotMachineController : MonoBehaviour
{
    public Image card1;
    public Image card2;
    public Image card3;
    public Button spinButton;
    public Sprite[] cardSprites;
    
    private bool isSpinning = false;
    private float spinDuration = 2f;
    private float spinSpeed = 0.1f;
    
    void Start()
    {
        if (spinButton != null)
        {
            spinButton.onClick.AddListener(OnSpinButtonClick);
        }
        
        // Load all card sprites from Resources
        cardSprites = Resources.LoadAll<Sprite>("CardImages");
        
        // If no sprites in Resources, try to load from Assets
        if (cardSprites == null || cardSprites.Length == 0)
        {
            cardSprites = new Sprite[5];
            for (int i = 0; i < 5; i++)
            {
                Texture2D tex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets/CardImages/card{i + 1}.jpg");
                if (tex != null)
                {
                    string path = UnityEditor.AssetDatabase.GetAssetPath(tex);
                    cardSprites[i] = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
                }
            }
        }
        
        // Set initial random cards
        SetRandomCard(card1);
        SetRandomCard(card2);
        SetRandomCard(card3);
    }
    
    void OnSpinButtonClick()
    {
        if (!isSpinning && cardSprites != null && cardSprites.Length > 0)
        {
            StartCoroutine(SpinRoutine());
        }
    }
    
    IEnumerator SpinRoutine()
    {
        isSpinning = true;
        spinButton.interactable = false;
        
        float elapsed = 0f;
        
        while (elapsed < spinDuration)
        {
            SetRandomCard(card1);
            SetRandomCard(card2);
            SetRandomCard(card3);
            
            yield return new WaitForSeconds(spinSpeed);
            elapsed += spinSpeed;
            
            // Slow down the spinning gradually
            spinSpeed = Mathf.Lerp(0.05f, 0.3f, elapsed / spinDuration);
        }
        
        // Final cards
        SetRandomCard(card1);
        SetRandomCard(card2);
        SetRandomCard(card3);
        
        spinSpeed = 0.1f;
        isSpinning = false;
        spinButton.interactable = true;
        
        Debug.Log("Spin complete!");
    }
    
    void SetRandomCard(Image cardImage)
    {
        if (cardImage != null && cardSprites != null && cardSprites.Length > 0)
        {
            int randomIndex = Random.Range(0, cardSprites.Length);
            if (cardSprites[randomIndex] != null)
            {
                cardImage.sprite = cardSprites[randomIndex];
            }
        }
    }
}