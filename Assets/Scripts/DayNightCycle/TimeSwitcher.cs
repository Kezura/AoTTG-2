﻿using Assets.Scripts.Services;
using UnityEngine;
using UnityEngine.UI;
using Assets.Scripts.Settings;
using UnityEngine.EventSystems;
using System;
namespace Assets.Scripts.DayNightCycle
{
    public class TimeSwitcher : MonoBehaviour, IEndDragHandler, IDragHandler
    {
        public Text Label;
        public Slider TimeSlider;
        public InputField TimeInput;
        DayAndNightControl dayNightCycle;

       void Start()
       {
            
            dayNightCycle = GameObject.Find("Day and Night Controller").GetComponent<DayAndNightControl>();
             if (PhotonNetwork.isMasterClient)
             {
                var se = new InputField.SubmitEvent();
                se.AddListener(SubmitTime);
                TimeInput.onEndEdit = se;
             }
            
        }


        public void OnDrag(PointerEventData eventData)
        {
            if (PhotonNetwork.isMasterClient)
            {
                dayNightCycle.currentTime = TimeSlider.value * 24;
                GameSettings.Time.currentTime = dayNightCycle.currentTime;
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (PhotonNetwork.isMasterClient && PhotonNetwork.connected)
            {
                dayNightCycle.currentTime = TimeSlider.value * 24;
                GameSettings.Time.currentTime = dayNightCycle.currentTime;
                Service.Settings.SyncSettings();
            }
        }

        void Update()
        {
            TimeSlider.value = dayNightCycle.CurrentTime01;
        }

        private void SubmitTime(string time)
        {
            if (TimeSpan.TryParse(time, out var timeSpan) && time.Contains(":"))
            {
                double seconds = timeSpan.TotalSeconds;
                TimeSlider.value = (float)(seconds / 86400);
                dayNightCycle.currentTime = (float)(24 * seconds / 86400);
                GameSettings.Time.currentTime = dayNightCycle.currentTime;
                Service.Settings.SyncSettings();
            }
        }


    

        //grabbing the local scene's DayAndNightControl script
        void OnEnable()
        {
            dayNightCycle = GameObject.Find("Day and Night Controller").GetComponent<DayAndNightControl>();
            TimeSlider.value = dayNightCycle.CurrentTime01;
        }

  
    }
   

}
