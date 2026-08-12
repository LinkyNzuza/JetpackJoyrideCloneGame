using UnityEngine;

public class BackgroundRecycler : MonoBehaviour
{
    public Transform[] backgroundPieces; 
    public Camera mainCamera;
    private float pieceWidth;

    void Start()
    {
        
        pieceWidth = backgroundPieces[0].GetComponent<SpriteRenderer>().bounds.size.x;
    }

    void Update()
    {
        foreach (Transform piece in backgroundPieces)
        {
            
            float distanceFromCamera = mainCamera.transform.position.x - piece.position.x;

            if (distanceFromCamera > pieceWidth)
            {
               
                float rightmostX = GetRightmostX();
                piece.position = new Vector3(rightmostX + pieceWidth, piece.position.y, piece.position.z);
            }
        }
    }

    float GetRightmostX()
    {
        float maxX = backgroundPieces[0].position.x;
        foreach (Transform piece in backgroundPieces)
        {
            if (piece.position.x > maxX)
                maxX = piece.position.x;
        }
        return maxX;
    }
}