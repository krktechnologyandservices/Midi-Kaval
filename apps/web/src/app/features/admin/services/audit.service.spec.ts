import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { AuditApiService } from './audit.service';
import { environment } from '../../../../environments/environment';

describe('AuditApiService', () => {
  let service: AuditApiService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [AuditApiService],
    });
    service = TestBed.inject(AuditApiService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
  });

  it('calls /api/v1/admin/audit (not /api/v1/audit)', () => {
    service.list({ page: 1, pageSize: 50 }).then(() => {});

    const req = http.expectOne((r) =>
      r.url === `${environment.apiBaseUrl}/api/v1/admin/audit`,
    );
    expect(req.request.method).toBe('GET');
    req.flush({ data: { items: [] }, meta: { requestId: 'test' } });
  });
});
