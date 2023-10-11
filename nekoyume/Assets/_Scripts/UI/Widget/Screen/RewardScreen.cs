﻿using System;
using System.Collections;

using UnityEngine;

namespace Nekoyume.UI
{
    using UniRx;
    public class RewardScreen : MailRewardScreen
    {
        private IDisposable _disposable;

        protected override IEnumerator PlayAnimation()
        {
            yield return new WaitUntil(() => AnimationState.Value == AnimationStateType.Shown);
            _isDone.SetValueAndForceNotify(true);
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            _disposable?.Dispose();
            _disposable = Observable
                .EveryUpdate()
                .Where(_ => _isDone.Value && Input.GetMouseButtonDown(0)).Subscribe(_ =>
                {
                    CloseWidget?.Invoke();
                });
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            _disposable.Dispose();
            _disposable = null;
        }
    }
}
