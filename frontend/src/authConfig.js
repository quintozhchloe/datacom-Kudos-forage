const region = import.meta.env.VITE_COGNITO_REGION;
const userPoolId = import.meta.env.VITE_COGNITO_USER_POOL_ID;
const clientId = import.meta.env.VITE_COGNITO_CLIENT_ID;
const domain = import.meta.env.VITE_COGNITO_DOMAIN;
const redirectUri = import.meta.env.VITE_COGNITO_REDIRECT_URI || window.location.origin;

const authority = `https://cognito-idp.${region}.amazonaws.com/${userPoolId}`;

export const oidcConfig = {
  authority,
  client_id: clientId,
  redirect_uri: redirectUri,
  response_type: 'code',
  scope: 'openid profile email',
  automaticSilentRenew: true,
  loadUserInfo: true,
  metadata: {
    issuer: authority,
    authorization_endpoint: `${domain}/oauth2/authorize`,
    token_endpoint: `${domain}/oauth2/token`,
    userinfo_endpoint: `${domain}/oauth2/userInfo`,
    jwks_uri: `${authority}/.well-known/jwks.json`,
    end_session_endpoint: `${domain}/logout`
  }
};
