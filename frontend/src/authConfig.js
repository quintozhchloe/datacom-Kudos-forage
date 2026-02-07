import { LogLevel } from '@azure/msal-browser';

const tenantId = import.meta.env.VITE_AAD_TENANT_ID;
const clientId = import.meta.env.VITE_AAD_CLIENT_ID;
const redirectUri = import.meta.env.VITE_AAD_REDIRECT_URI || window.location.origin;
const scope = import.meta.env.VITE_AAD_SCOPE || `api://${clientId}/user_impersonation`;

export const msalConfig = {
  auth: {
    clientId,
    authority: `https://login.microsoftonline.com/${tenantId}`,
    redirectUri
  },
  cache: {
    cacheLocation: 'localStorage',
    storeAuthStateInCookie: false
  },
  system: {
    loggerOptions: {
      logLevel: LogLevel.Warning
    }
  }
};

export const loginRequest = {
  scopes: [scope]
};
