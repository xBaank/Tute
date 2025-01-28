using UnityEngine;

namespace Assets.Scripts
{
    internal class CardAnim : MonoBehaviour
    {
        private Animator animator;
        private void Start()
        {
            animator = GetComponent<Animator>();
        }

        private void OnMouseOver()
        {
            animator.SetBool("IsOver", true);
        }

        private void OnMouseExit()
        {
            animator.SetBool("IsOver", false);
        }
    }
}
