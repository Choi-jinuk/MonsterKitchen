using UnityEngine;

namespace MonsterKitchen.Restaurant
{
    /// <summary>
    /// 식당 테이블 1개. 손님 착석 여부를 관리한다.
    /// </summary>
    public class RestaurantTable : MonoBehaviour
    {
        [SerializeField] Transform seatPoint;   // 손님이 앉을 위치

        public bool       IsOccupied  { get; private set; }
        public CustomerAI Occupant    { get; private set; }

        public Transform SeatPoint => seatPoint != null ? seatPoint : transform;

        public void Occupy(CustomerAI customer)
        {
            IsOccupied = true;
            Occupant   = customer;
        }

        public void Vacate()
        {
            IsOccupied = false;
            Occupant   = null;
        }
    }
}
