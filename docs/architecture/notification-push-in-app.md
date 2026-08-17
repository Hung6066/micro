# Notification: mobile push + in-app

His.Hope uses one durable notification write path:

1. An authorized producer calls `POST /api/v1/admin/push/notifications`.
2. Identity Service writes `in_app_notifications` and `push_notification_outbox` in the same database transaction.
3. The mobile app reads `GET /api/v1/mobile/notifications` and marks items through the read endpoints.
4. `PushNotificationOutboxWorker` retries delivery to registered FCM/APNs devices; an inbox item is retained when push is unavailable.

## Mobile endpoints

- `POST /api/v1/mobile/push-tokens`
- `GET /api/v1/mobile/notifications?page=1&pageSize=30`
- `POST /api/v1/mobile/notifications/{id}/read`
- `POST /api/v1/mobile/notifications/read-all`

All endpoints require an authenticated `sub` claim in the handler. The mobile route is intentionally `AllowAnonymous` at the middleware boundary so native clients receive JSON 401 instead of a browser redirect.

## Provider gate

FCM and APNs credentials must be supplied from Vault/environment configuration. No provider secret belongs in the repository. Android requires a real FCM project/device token; iOS requires APNs key material and the production bundle identifier. Without those, in-app remains usable and the push outbox stays retryable.
