import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '@env/environment';

export interface CreateJobFromExistingRequest {
  bucket: string;
  filePath: string;
  documentVersionId?: string;
  totalPages: number;
}

export interface CreateJobResponse {
  jobId: string;
  regionCount: number;
  state: string;
}

export interface OcrModuleRegionDto {
  id: string;
  pageNumber: number;
  boxX0: number;
  boxY0: number;
  boxX1: number;
  boxY1: number;
  textRaw: string;
  confidence?: number;
  scriptType?: string;
  regionType: string;
  formulaText?: string;
  sealSignatureScore?: number;
  spellcheckSuggestion?: string;
  spellcheckStatus?: string;
  status: string;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface ScriptTypeClassifyResponse {
  totalRegions: number;
  printedCount: number;
  handwrittenCount: number;
  mixedCount: number;
}

export interface FormulaRunResponse {
  totalRegions: number;
  formulaRegionCount: number;
}

export interface SealSignatureRunResponse {
  sealCount: number;
  signatureCount: number;
}

export interface OcrModuleTemplateSnapshot {
  id: string;
  name: string;
  documentTypeCode?: string;
  sourceJobId?: string;
  createdBy?: string;
  createdDate?: string;
}

export interface CreateTemplateSnapshotRequest {
  name: string;
  documentTypeCode?: string;
  sourceJobId: string;
}

export interface TemplateDiffRunResponse {
  totalDiffs: number;
  missingCount: number;
  extraCount: number;
  textMismatchCount: number;
  positionShiftCount: number;
}

export interface OcrModuleTemplateDiffResult {
  id: string;
  jobId: string;
  templateSnapshotId: string;
  regionId?: string;
  diffType: string;
  detail?: string;
  status: string;
  pageNumber: number;
}

export interface SpellcheckRunResult {
  totalRegionsChecked: number;
  suggestionCount: number;
}

export interface OcrModuleErrorAnalysis {
  id: string;
  jobId: string;
  regionId?: string;
  pageNumber: number;
  errorCategory: string;
  severity: string;
  detail?: string;
  resolvedStatus: string;
}

@Injectable({ providedIn: 'root' })
export class OcrModuleService {
  private readonly baseUrl = `${environment.apiGatewayUrl}/api/v1/ocr-module`;

  constructor(private http: HttpClient) {}

  createJobFromExisting(payload: CreateJobFromExistingRequest): Observable<CreateJobResponse> {
    return this.http.post<CreateJobResponse>(`${this.baseUrl}/jobs/from-existing`, payload);
  }

  getRegions(jobId: string, page = 1, pageSize = 50): Observable<PagedResult<OcrModuleRegionDto>> {
    return this.http.get<PagedResult<OcrModuleRegionDto>>(`${this.baseUrl}/jobs/${jobId}/regions`, {
      params: { page, pageSize },
    });
  }

  getPageImage(jobId: string, pageNumber: number): Observable<Blob> {
    return this.http.get(`${this.baseUrl}/jobs/${jobId}/pages/${pageNumber}/image`, { responseType: 'blob' });
  }

  classifyScriptType(jobId: string, pageNumber?: number): Observable<ScriptTypeClassifyResponse> {
    return this.http.post<ScriptTypeClassifyResponse>(
      `${this.baseUrl}/jobs/${jobId}/script-type/classify`,
      {},
      { params: pageNumber != null ? { pageNumber } : {} },
    );
  }

  runFormulaRecognition(jobId: string, pageNumber?: number): Observable<FormulaRunResponse> {
    return this.http.post<FormulaRunResponse>(
      `${this.baseUrl}/jobs/${jobId}/formula/run`,
      {},
      { params: pageNumber != null ? { pageNumber } : {} },
    );
  }

  runSealSignatureDetection(jobId: string, pageNumber?: number): Observable<SealSignatureRunResponse> {
    return this.http.post<SealSignatureRunResponse>(
      `${this.baseUrl}/jobs/${jobId}/seal-signature/run`,
      {},
      { params: pageNumber != null ? { pageNumber } : {} },
    );
  }

  getTemplates(): Observable<OcrModuleTemplateSnapshot[]> {
    return this.http.get<OcrModuleTemplateSnapshot[]>(`${this.baseUrl}/templates`);
  }

  createTemplate(payload: CreateTemplateSnapshotRequest): Observable<OcrModuleTemplateSnapshot> {
    return this.http.post<OcrModuleTemplateSnapshot>(`${this.baseUrl}/templates`, payload);
  }

  runTemplateDiff(jobId: string, templateSnapshotId: string, pageNumber?: number): Observable<TemplateDiffRunResponse> {
    return this.http.post<TemplateDiffRunResponse>(`${this.baseUrl}/jobs/${jobId}/template-diff/run`, {
      templateSnapshotId,
      pageNumber: pageNumber ?? null,
    });
  }

  getTemplateDiffResults(jobId: string): Observable<OcrModuleTemplateDiffResult[]> {
    return this.http.get<OcrModuleTemplateDiffResult[]>(`${this.baseUrl}/jobs/${jobId}/template-diff/results`);
  }

  runSpellcheck(jobId: string, pageNumber?: number): Observable<SpellcheckRunResult> {
    return this.http.post<SpellcheckRunResult>(
      `${this.baseUrl}/jobs/${jobId}/spellcheck/run`,
      {},
      { params: pageNumber != null ? { pageNumber } : {} },
    );
  }

  updateSpellcheckStatus(
    jobId: string,
    regionId: string,
    payload: { status: 'Accepted' | 'Rejected' | 'ManuallyEdited'; suggestionText?: string; manualText?: string },
  ): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/jobs/${jobId}/regions/${regionId}/spellcheck`, payload);
  }

  runErrorAnalysis(jobId: string, pageNumber?: number): Observable<OcrModuleErrorAnalysis[]> {
    return this.http.post<OcrModuleErrorAnalysis[]>(
      `${this.baseUrl}/jobs/${jobId}/error-analysis/run`,
      {},
      { params: pageNumber != null ? { pageNumber } : {} },
    );
  }

  resolveErrorAnalysis(jobId: string, errorId: string): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/jobs/${jobId}/error-analysis/${errorId}/resolve`, {});
  }
}
