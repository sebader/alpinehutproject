import { processErrorResponseAsync } from "../utils";

const API_ENDPOINT = window.API_URL;

export default class NotificationService {
   async subscribeFreeBedNotificationAsync(hutId, emailAddress, date) {
      const res = await fetch(`${API_ENDPOINT}/freebednotifications/${hutId}`, {
         method: "POST",
         headers: {
            "Content-Type": "application/json",
         },
         body: JSON.stringify({ emailAddress, date }),
      });

      if (!res.ok) {
         throw new Error(await processErrorResponseAsync(res));
      }
   }
}
