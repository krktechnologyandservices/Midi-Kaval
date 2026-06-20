import { HttpContextToken } from '@angular/common/http';

export const AUTH_RETRY_ATTEMPT = new HttpContextToken<boolean>(() => false);
