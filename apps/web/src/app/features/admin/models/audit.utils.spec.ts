import { EVENT_TYPE_LABELS, formatEventType } from './audit.utils';

describe('audit.utils', () => {
  describe('formatEventType', () => {
    it('returns label for known event types', () => {
      expect(formatEventType('auth.login.success')).toBe('Login success');
      expect(formatEventType('case.created')).toBe('Case created');
      expect(formatEventType('court.sitting.created')).toBe('Court sitting created');
    });

    it('returns label for role management event types', () => {
      expect(formatEventType('user.suspended')).toBe('User Suspended');
      expect(formatEventType('user.reactivated')).toBe('User Reactivated');
      expect(formatEventType('user.deleted')).toBe('User Deleted');
      expect(formatEventType('invitation.sent')).toBe('Invitation Sent');
      expect(formatEventType('invitation.resent')).toBe('Invitation Resent');
      expect(formatEventType('invitation.resent_notified')).toBe('Inviter Notified of Resend');
      expect(formatEventType('user.two_factor_provisioned')).toBe('2FA Provisioned');
      expect(formatEventType('user.two_factor_enrolled')).toBe('2FA Enrolled');
      expect(formatEventType('user.two_factor_reset')).toBe('2FA Reset');
      expect(formatEventType('activation_reissued')).toBe('Activation Reissued');
      expect(formatEventType('organisation.activated')).toBe('Organisation Activated');
    });

    it('returns raw event type for unknown types', () => {
      expect(formatEventType('some.unknown.event')).toBe('some.unknown.event');
    });

    it('handles empty string', () => {
      expect(formatEventType('')).toBe('');
    });
  });

  describe('EVENT_TYPE_LABELS', () => {
    it('contains all expected role management entries', () => {
      expect(EVENT_TYPE_LABELS['user.suspended']).toBeDefined();
      expect(EVENT_TYPE_LABELS['user.reactivated']).toBeDefined();
      expect(EVENT_TYPE_LABELS['user.deleted']).toBeDefined();
      expect(EVENT_TYPE_LABELS['invitation.sent']).toBeDefined();
      expect(EVENT_TYPE_LABELS['invitation.resent']).toBeDefined();
      expect(EVENT_TYPE_LABELS['invitation.resent_notified']).toBeDefined();
      expect(EVENT_TYPE_LABELS['user.two_factor_provisioned']).toBeDefined();
      expect(EVENT_TYPE_LABELS['user.two_factor_enrolled']).toBeDefined();
      expect(EVENT_TYPE_LABELS['user.two_factor_reset']).toBeDefined();
      expect(EVENT_TYPE_LABELS['activation_reissued']).toBeDefined();
      expect(EVENT_TYPE_LABELS['organisation.activated']).toBeDefined();
    });
  });
});
